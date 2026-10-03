/** 
 * <feature scope="SanteDB.Persistence.Data" id="20261002-02" name="Update:20261002-02"   invariantName="npgsql" >
 *	<summary>Update: Extends the observations table</summary>
 *	<isInstalled>select ck_patch('20261002-02')</isInstalled>
 * </feature>
 */
 
 CREATE TABLE uri_obs_tbl (
	act_vrsn_id UUID NOT NULL,
	ct_cls_cd_id UUID NOT NULL, 
	mime VARCHAR(256) NOT NULL,
	hash BYTEA,
	val_uri VARCHAR(1024) NOT NULL,
	avail_start_utc TIMESTAMPTZ, 
	avail_stop_utc TIMESTAMPTZ CHECK (avail_stop_utc IS NULL OR (avail_start_utc IS NULL OR avail_start_utc < avail_stop_utc)),
	CONSTRAINT pk_uri_obs_tbl PRIMARY KEY (act_vrsn_id),
	CONSTRAINT fk_uri_obs_obs_tbl FOREIGN KEY (act_vrsn_id) REFERENCES obs_tbl(act_vrsn_id),
	CONSTRAINT fk_uri_obs_ct_cls_cd FOREIGN KEY (ct_cls_cd_id) REFERENCES cd_tbl(cd_id),
	CONSTRAINT ck_uri_obs_ct_cls_cd CHECK (CK_IS_CD_SET_MEM(ct_cls_cd_id, 'ObservationClassCodes', TRUE))
 );

 CREATE TABLE bl_obs_tbl (
	act_vrsn_id UUID NOT NULL,
	val_bl BOOLEAN NOT NULL,
	CONSTRAINT pk_bl_obs_tbl PRIMARY KEY (act_vrsn_id),
	CONSTRAINT fk_bl_obs_obs_tbl FOREIGN KEY (act_vrsn_id) REFERENCES obs_tbl(act_vrsn_id)
);

CREATE TABLE nm_obs_tbl (
	act_vrsn_id UUID NOT NULL,
	val_nm NUMERIC(20,8) NOT NULL,
	CONSTRAINT pk_nm_obs_tbl PRIMARY KEY (act_vrsn_id),
	CONSTRAINT fk_nm_obs_obs_tbl FOREIGN KEY (act_vrsn_id) REFERENCES obs_tbl(act_vrsn_id)
);

ALTER TABLE obs_tbl DROP CONSTRAINT ck_obs_val_typ ;
ALTER TABLE obs_tbl ADD CONSTRAINT ck_obs_val_typ CHECK (val_typ IN ('NA','ST','PQ','CD','TS','NM','BL','UR'));



 SELECT REG_PATCH('20261002-02');