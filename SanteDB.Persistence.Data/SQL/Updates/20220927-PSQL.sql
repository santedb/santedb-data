/** 
 * <feature scope="SanteDB.Persistence.Data" id="20220927-01" name="Update:20220927-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="npgsql">
 *	<summary>Update: Adds device and application claims</summary>
 *	<isInstalled>select ck_patch('20220927-01')</isInstalled>
 * </feature>
 */
 
CREATE TABLE SEC_APP_CLM_TBL
(
	clm_id uuid NOT NULL,
	app_id uuid NOT NULL,
	clm_typ varchar(128) NOT NULL,
	clm_val varchar(128) NOT NULL,
	exp_utc timestamptz NULL,
	CONSTRAINT pk_sec_app_clm_tbl PRIMARY KEY (clm_id),
	CONSTRAINT fk_sec_app_clm_tbl FOREIGN KEY (app_id) REFERENCES sec_app_tbl(app_id)
);
CREATE INDEX sec_app_clm_app_id_idx ON sec_app_clm_tbl USING btree (app_id);
CREATE TABLE SEC_DEV_CLM_TBL
(
	clm_id uuid NOT NULL,
	dev_id uuid NOT NULL,
	clm_typ varchar(128) NOT NULL,
	clm_val varchar(128) NOT NULL,
	exp_utc timestamptz NULL,
	CONSTRAINT pk_sec_dev_clm_tbl PRIMARY KEY (clm_id),
	CONSTRAINT fk_sec_dev_clm_tbl FOREIGN KEY (dev_id) REFERENCES sec_dev_tbl(dev_id)
);--#!
CREATE INDEX sec_dev_clm_dev_id_idx ON sec_dev_clm_tbl USING btree (dev_id);
SELECT REG_PATCH('20220927-01'); 