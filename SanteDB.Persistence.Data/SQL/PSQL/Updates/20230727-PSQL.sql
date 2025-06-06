/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230727-01" name="Update:20230727-01"   invariantName="npgsql">
 *	<summary>Update: Adds parameters to the foreign data manager</summary>
 *	<isInstalled>select ck_patch('20230727-01')</isInstalled>
 * </feature>
 */
ALTER TABLE SEC_CER_TBL ALTER COLUMN X509_THB TYPE VARCHAR(64);
CREATE TABLE FD_STG_PARM_SYSTBL (
	FD_PARM_ID UUID NOT NULL DEFAULT uuid_generate_v1(),
	FD_ID UUID NOT NULL, 
	PARM_NAME VARCHAR(64) NOT NULL,
	PARM_VAL VARCHAR(256) NOT NULL,
	CONSTRAINT PK_FD_STG_PARM_SYSTBL PRIMARY KEY (FD_PARM_ID),
	CONSTRAINT FK_FD_STG_PARM_STG_SYSTBL FOREIGN KEY (FD_ID) REFERENCES FD_STG_SYSTBL(FD_ID)
);
CREATE UNIQUE INDEX FD_STG_PARM_UQ_IDX ON FD_STG_PARM_SYSTBL(FD_ID, PARM_NAME);

-- CHANGE INDEX TYPE ON NAMES
DROP INDEX IF EXISTS ref_term_name_term_name_idx;
CREATE INDEX ref_term_name_term_name_idx ON ref_term_name_tbl USING gin (term_name gin_trgm_ops);
DROP INDEX IF EXISTS cd_name_val_idx ;
CREATE INDEX cd_name_val_idx ON cd_name_tbl USING gin (val gin_trgm_ops);

SELECT REG_PATCH('20230727-01'); 
