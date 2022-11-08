/** 
 * <feature scope="SanteDB.Persistence.Data" id="20220927-01" name="Update:20220927-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="FirebirdSQL">
 *	<summary>Update: Adds claims to applications and devices</summary>
 *	<isInstalled>select ck_patch('20220927-01') from RDB$DATABASE</isInstalled>
 * </feature>
 */

CREATE TABLE SEC_APP_CLM_TBL
(
	clm_id uuid NOT NULL,
	app_id uuid NOT NULL,
	clm_typ varchar(128) NOT NULL,
	clm_val varchar(128) NOT NULL,
	exp_utc timestamp,
	CONSTRAINT pk_sec_app_clm_tbl PRIMARY KEY (clm_id),
	CONSTRAINT fk_sec_app_clm_tbl FOREIGN KEY (app_id) REFERENCES sec_app_tbl(app_id)
);--#!
CREATE INDEX sec_app_clm_app_id_idx ON sec_app_clm_tbl (app_id);--#!
CREATE TABLE SEC_DEV_CLM_TBL
(
	clm_id uuid NOT NULL,
	dev_id uuid NOT NULL,
	clm_typ varchar(128) NOT NULL,
	clm_val varchar(128) NOT NULL,
	exp_utc timestamp,
	CONSTRAINT pk_sec_dev_clm_tbl PRIMARY KEY (clm_id),
	CONSTRAINT fk_sec_dev_clm_tbl FOREIGN KEY (dev_id) REFERENCES sec_dev_tbl(dev_id)
);--#!
CREATE INDEX sec_dev_clm_dev_id_idx ON sec_dev_clm_tbl (dev_id);--#!
UPDATE ent_vrsn_tbl SET cls_cd_id = char_to_uuid('6a2b00ba-501b-4523-b57c-f96d8ae44684') WHERE ent_id = char_to_uuid('b55f0836-40e6-4ee2-9522-27e3f8bfe532');--#!

SELECT REG_PATCH('20220927-01') FROM RDB$DATABASE; --#!

