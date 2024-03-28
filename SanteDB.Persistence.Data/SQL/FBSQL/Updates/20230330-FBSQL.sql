/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230330-01" name="Update:20230330-01"   invariantName="FirebirdSQL">
 *	<summary>Update: Adds concept set references to the database</summary>
 *	<isInstalled>select ck_patch('20230330-01') FROM RDB$DATABASE</isInstalled>
 * </feature>
 */
 
 CREATE TABLE cd_set_comp_assoc_tbl (
	SET_COMP_ID UUID NOT NULL,
	SET_ID UUID NOT NULL,
	TRG_SET_ID UUID NOT NULL,
	ROL_CS INT NOT NULL CHECK(ROL_CS IN (1,2)),
	CONSTRAINT PK_CD_SET_COMP_ASSOC_TBL PRIMARY KEY (SET_COMP_ID),
	CONSTRAINT FK_CD_SET_COMP_ASSOC_SRC FOREIGN KEY (SET_ID) REFERENCES CD_SET_TBL(SET_ID),
	CONSTRAINT FK_CD_SET_COMP_ASSOC_TRG FOREIGN KEY (TRG_SET_ID) REFERENCES CD_SET_TBL(SET_ID)
);--#!

SELECT REG_PATCH('20230330-01') FROM RDB$DATABASE; --#!