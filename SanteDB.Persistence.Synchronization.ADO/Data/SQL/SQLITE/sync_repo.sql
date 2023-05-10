/** 
 * <feature scope="SanteDB.Persistence.Synchronization.ADO" id="00010000-00" name="Initialize:001-01" invariantName="sqlite">
 *	<summary>Installs the core schema for SanteDB Synchronization Repository</summary>
 *	<remarks>This script installs the necessary core schema files for SanteDB</remarks>
 *  <isInstalled mustSucceed="true">SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type='table' AND name='SYNC_LOG_TBL')</isInstalled>
 * </feature>
 */

 CREATE TABLE SYNC_LOG_TBL (
	ID BLOB(16) NOT NULL DEFAULT (RANDOMBLOB(16)),
	RES_TYP VARCHAR(256) NOT NULL,
	LST_SYNC_UTC BIGINT NULL,
	LST_ETAG VARCHAR(256),
	FLTR VARCHAR(512),
	QRY_ID BLOB(16) NULL,
	QRY_OFFST INT NULL,
	QRY_STRT_UTC BIGINT NULL,
	LAST_ERR TEXT,
	CONSTRAINT PK_SYNC_LOG_TBL PRIMARY KEY (ID)
 ); --#!

 CREATE INDEX SYNC_LOG_RES_TYPE_FLTR_QRY_ID_IDX ON SYNC_LOG_TBL(RES_TYP, FLTR, QRY_ID); --#!