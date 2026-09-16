# Full application database backup

Admin → Архив на базата exports an unfiltered, compressed PostgreSQL custom archive.
It includes every application schema/table/row, Identity records, EF migration history,
functions, sequences, views, indexes, constraints and PostgreSQL large objects, including
objects not mapped by EF. It uses the same resolved connection as the application.

Restore accepts a trusted `.dump` and requires the literal confirmation `RESTORE`.
Archive validation/decompression completes before changing the database. Schema cleanup
and archive replay execute in one PostgreSQL transaction with stop-on-error. New schemas
and tables absent from the snapshot are removed. A failure rolls back database changes.
Downloads and uploads use temporary disk files cleaned up after completion; ensure enough
free space for both the compressed archive and its expanded SQL. Operations are serialized
within an application process. Schedule restores during a quiet maintenance window.

The runtime installs PostgreSQL 18 client tools (`pg_dump`, `pg_restore`, `psql`).
Local development requires these tools in PATH, at least as new as the database server.
The configured database user must own/manage the application's database objects.
Object owners and PostgreSQL ACLs are not replayed, for managed-database compatibility.
Cluster roles, other databases, server configuration, environment secrets and external
media (Cloudinary/filesystem) are outside this application-database archive. Media URLs
and metadata stored in PostgreSQL are included. Keep the external files separately.
Only restore trusted full archives created from this application database. Do not submit
partial/schema-only dumps. Restoring an older schema may require deploying matching code.

CI provisions isolated PostgreSQL 17 and tests full round-trip restoration, extra-object
removal, relationships, sequence values, binary data, large objects and transactional
rollback after a deliberate restore failure. It never accesses the production database.
