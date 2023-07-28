/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230727-01" name="Update:20230727-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="FirebirdSQL">
 *	<summary>Update: Adds foreign data staging parameter tables</summary>
 *	<isInstalled>select ck_patch('20230727-01') from rdb$database</isInstalled>
 * </feature>
 */
CREATE TABLE FD_STG_PARM_SYSTBL (
	FD_PARM_ID UUID NOT NULL,
	FD_ID UUID NOT NULL, 
	PARM_NAME VARCHAR(64) NOT NULL,
	PARM_VAL VARCHAR(256) NOT NULL,
	CONSTRAINT PK_FD_STG_PARM_SYSTBL PRIMARY KEY (FD_PARM_ID),
	CONSTRAINT FK_FD_STG_PARM_STG_SYSTBL FOREIGN KEY (FD_ID) REFERENCES FD_STG_SYSTBL(FD_ID)
);--#!
CREATE UNIQUE INDEX FD_STG_PARM_UQ_IDX ON FD_STG_PARM_SYSTBL(FD_ID, PARM_NAME);--#!
SELECT REG_PATCH('20230727-01') FROM RDB$DATABASE; --#!
