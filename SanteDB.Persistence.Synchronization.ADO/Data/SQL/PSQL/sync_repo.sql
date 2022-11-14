/** 
 * <feature scope="SanteDB.Persistence.Synchronization.ADO" id="00010000-00" name="Initialize:001-01" invariantName="npgsql">
 *	<summary>Installs the core schema for SanteDB Synchronization Repository</summary>
 *	<remarks>This script installs the necessary core schema files for SanteDB</remarks>
 *  <isInstalled mustSucceed="true">SELECT to_regclass('public.sync_log_tbl') IS NOT NULL;</isInstalled>
 * </feature>
 */

 --adds uuid_generate_v4() to the database
 CREATE EXTENSION IF NOT EXISTS "uuid-ossp";

 CREATE TABLE "sync_log_tbl" (
	id uuid not null default uuid_generate_v4(),
	res_typ character varying(256) not null,
	lst_sync_utc timestamp with time zone,
	lst_etag character varying(256),
	fltr character varying(512),
	qry_id uuid,
	qry_offst integer,
	qry_strt_utc timestamp with time zone,
	CONSTRAINT pk_sync_log_tbl PRIMARY KEY (id)
 ); --#!

 CREATE INDEX IF NOT EXISTS sync_log_res_typ_fltr_qry_id_idx ON sync_log_tbl(res_typ, fltr, qry_id); --#!