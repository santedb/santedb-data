/** 
 * <feature scope="SanteDB.Persistence.Data" id="20261002-01" name="Update:20261002-01"   invariantName="sqlite"  >
 *	<summary>Update: Extends observation tables</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM patch_db_systbl WHERE patch_id='20261002-01')</isInstalled>
 * </feature>
 */


 
 CREATE TABLE uri_obs_tbl (
	act_vrsn_id blob(16) NOT NULL,
	ct_cls_cd_id blob(16) NOT NULL, 
	mime VARCHAR(256) NOT NULL,
	hash BYTEA,
	val_uri VARCHAR(MAX) NOT NULL,
	avail_start_utc TIMESTAMPTZ, 
	avail_stop_utc BIGINT CHECK (avail_stop_utc IS NULL OR (avail_start_utc IS NULL OR avail_start_utc < avail_stop_utc))
	CONSTRAINT pk_uri_obs_tbl PRIMARY KEY (act_vrsn_id),
	CONSTRAINT fk_uri_obs_obs_tbl FOREIGN KEY (act_vrsn_id) REFERENCES obs_tbl(act_vrsn_id),
	CONSTRAINT fk_uri_obs_ct_cls_cd FOREIGN KEY (ct_cls_cd_id) REFERENCES cd_tbl(cd_id)
 );

 CREATE TABLE bl_obs_tbl (
	act_vrsn_id blob(16) NOT NULL,
	val_bl BOOLEAN NOT NULL,
	CONSTRAINT pk_bl_obs_tbl PRIMARY KEY (act_vrsn_id),
	CONSTRAINT fk_bl_obs_obs_tbl FOREIGN KEY (act_vrsn_id) REFERENCES obs_tbl(act_vrsn_id)
);

CREATE TABLE nm_obs_tbl (
	act_vrsn_id blob(16) NOT NULL,
	val_nm NUMERIC(20,8) NOT NULL,
	CONSTRAINT pk_nm_obs_tbl PRIMARY KEY (act_vrsn_id),
	CONSTRAINT fk_nm_obs_obs_tbl FOREIGN KEY (act_vrsn_id) REFERENCES obs_tbl(act_vrsn_id)
);

ALTER TABLE OBS_TBL ADD VAL_TYP_N VARCHAR(2);
UPDATE OBS_TBL SET VAL_TYP_N = VAL_TYP;
DROP INDEX OBS_VAL_TYP_IDX;
ALTER TABLE OBS_TBL DROP VAL_TYP;
ALTER TABLE OBS_TBL ADD VAL_TYP VARCHAR(2) CHECK (val_typ IN ('NA','ST','PQ','CD','TS','NM','BL','UR'));
UPDATE OBS_TBL SET VAL_TYP = VAL_TYP_N;
ALTER TABLE OBS_TBL DROP VAL_TYP_N;
CREATE INDEX OBS_VAL_TYP_IDX ON OBS_TBL(VAL_TYP);

INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20261002-01', UNIXEPOCH(), 'Adds additional observation tables');--#!
