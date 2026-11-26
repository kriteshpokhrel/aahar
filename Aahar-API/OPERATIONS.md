# Backend operations and recovery

This is an operator guide, not proof of a tested deployment. **Creating, restoring, or migrating a database requires explicit approval and an independently verified dedicated target.** Never use an existing unrelated database. The application does not create or migrate a database at startup; bootstrapping the first admin does write to the configured database. No database was accessed to prepare this guide.

## Before starting

1. Provision the dedicated PostgreSQL database only after approval. Keep the connection string private in `ConnectionStrings__Aahar`; do not commit it or share it in chat. Restrict PostgreSQL and operating-system access to the intended operator and application.
2. Select `Business__TimeZone`, keep system time-zone data current, and protect the data-protection key store and HTTPS certificate. Keep the frontend and API on localhost; configure the frontend as a same-origin HTTPS `/api` proxy. See [security assumptions](SECURITY.md).
3. Review the migrations and generated SQL before applying anything. Follow the [setup commands](BACKEND.md#setup) for an approved target; the design-time factory's placeholder is for offline tooling, not a real database target.
4. After the approved migration, bootstrap the administrator once and start the application as described in the setup guide. Check login, role access, CSRF, menu, stock, and simulated order/payment flows using authorized test data before operational use.

## Migration precautions

Stop application instances and other writers before maintenance; confirm server, database, and account using an independent administrative channel. Take and verify an authorized backup before an upgrade, and keep the exact application revision, migration history, time-zone setting, PostgreSQL version, and restoration procedure with its metadata. EF SQL script generation is offline, but `dotnet ef database update` connects to and modifies its configured target: run it only after approval and target verification.

The initial PostgreSQL migration is intended for a dedicated empty database, not an import from the old SQL Server scaffold. Later migrations refuse destructive rollback; the initial migration's `Down` drops tables. Prefer an approved restore to a separate dedicated target rather than running `Down`. The ledger migration refuses duplicate active table orders and records only an opening balance for existing stock. The kitchen migration reconstructs queued tickets for old Sent orders; an operator must check their actual preparation state before acting. Existing submitted pre-ledger orders cannot be automatically cancelled. The inventory migration initializes existing thresholds to zero and preserves old movement reasons as null. See [setup](BACKEND.md#setup) for details.

## Backup

Backups are not automated by this application. Establish a schedule, retention, encryption, off-machine storage, access controls, and an owner; database dumps contain sensitive operational data and password hashes. Choose PostgreSQL client tools compatible with the source server and the intended restore server; check PostgreSQL's version compatibility guidance. A database-only `pg_dump` does not back up cluster-wide roles, external data-protection keys, certificates, or application configuration. Preserve those separately under restricted access and record the application and database versions and backup time. Decide how much data loss is acceptable between backups.

After authorization, and only after independently verifying the source connection and the intended new output path, an operator can use PostgreSQL's custom archive format:

```bash
: "${PGHOST:?Set verified PGHOST}" "${PGPORT:?Set verified PGPORT}" "${PGUSER:?Set verified PGUSER}" "${PGDATABASE:?Set verified PGDATABASE}" &&
pg_dump --format=custom --file=/secure/path/new-aahar-backup.dump --dbname="$PGDATABASE"
```

Replace the example path with a unique protected path and set `PGDATABASE` to the database name, not a connection string. Set host, port, user, and credentials privately before running. The presence checks do not verify identity: verify the source independently, and do not put passwords in command arguments. `pg_dump` can overwrite an existing output file. Inspect the archive offline with `pg_restore --list /secure/path/new-aahar-backup.dump` and check its contents and storage integrity. A listing is not a restore test. Consult [PostgreSQL backup documentation](https://www.postgresql.org/docs/current/backup-dump.html) for privileges and version compatibility.

## Restore and validation

1. Obtain explicit approval to create or use a **separate, dedicated empty target**, and independently verify its server, database, and account. Stop writers to that target. Do not restore over an existing database or use `--clean` or `--create` as a shortcut. Keep the original and its backups intact.
2. Check archive provenance and compatibility, inspect its table of contents, and ensure the restore account can create the required objects. A dump can contain SQL chosen by its source; only restore a trusted archive. Configure host, port, credentials and `PGDATABASE` privately for the approved target.
3. For a trusted custom-format archive into that verified empty target, the following is an example, not a verified recovery run:

   ```bash
   : "${PGHOST:?Set verified PGHOST}" "${PGPORT:?Set verified PGPORT}" "${PGUSER:?Set verified PGUSER}" "${PGDATABASE:?Set verified PGDATABASE}" &&
   pg_restore --exit-on-error --single-transaction --no-owner --no-privileges --dbname="$PGDATABASE" /secure/path/new-aahar-backup.dump
   ```

   Replace the path with the verified archive and use only a database name for `PGDATABASE`. The presence checks do not verify the target. `--no-owner` and `--no-privileges` do not recreate original owners or grants; an administrator must review and apply the intended access separately. If restore fails, stop and investigate; do not blindly retry against a partially changed target. Consult [pg_restore documentation](https://www.postgresql.org/docs/current/app-pgrestore.html).
4. With approval, validate the restored migration history against the application version, required grants, counts and representative orders, payments, stock balances and ledger records. Test sign-in and workflows with authorized test accounts; reconcile work performed after the backup, including kitchen tickets and physical stock, before allowing operations. Restoring a database can restore older accounts and state. Data-protection keys are separate: lost or changed keys can invalidate cookies, while retaining older keys can leave old cookies usable until expiration.
5. Only after review and explicit approval, decide whether to promote the restored target and restart the application. Protect or securely dispose of superseded copies according to the retention policy. Record the outcome and periodically repeat restore drills on separately approved disposable targets.

This repository's tests and offline migration generation do not verify a backup, restore, live migration, PostgreSQL concurrency, or disaster recovery. No database commands above have been exercised against a live server for this guide.
