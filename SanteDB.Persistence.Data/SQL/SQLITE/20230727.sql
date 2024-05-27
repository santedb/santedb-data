/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230727-01" name="Update:20230727-01"   invariantName="sqlite">
 *	<summary>Update: Adds foreign data staging table parameter</summary>
 *	<isInstalled>select count(*) > 0 from patch_db_systbl WHERE PATCH_ID = '20230727-01'</isInstalled>
 * </feature>
 */
 CREATE TABLE FD_STG_PARM_SYSTBL (
	FD_PARM_ID UUID DEFAULT (randomblob(16)) NOT NULL,
	FD_ID UUID NOT NULL, 
	PARM_NAME VARCHAR(64) NOT NULL,
	PARM_VAL VARCHAR(256) NOT NULL,
	CONSTRAINT PK_FD_STG_PARM_SYSTBL PRIMARY KEY (FD_PARM_ID),
	CONSTRAINT FK_FD_STG_PARM_STG_SYSTBL FOREIGN KEY (FD_ID) REFERENCES FD_STG_SYSTBL(FD_ID)
);
CREATE UNIQUE INDEX FD_STG_PARM_UQ_IDX ON FD_STG_PARM_SYSTBL(FD_ID, PARM_NAME);
INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20230727-01', UNIXEPOCH(), 'Adds foreign data staging table'); 
