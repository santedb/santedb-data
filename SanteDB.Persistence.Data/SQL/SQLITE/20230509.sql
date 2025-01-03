/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230509-02" name="Update:20230509-02"   invariantName="sqlite">
 *	<summary>Update: Adds external tagging / key tracking to the database</summary>
 *	<isInstalled>select count(*) > 0 from patch_db_systbl WHERE PATCH_ID = '20230509-02'</isInstalled>
 * </feature>
 */
 -- OPTIONAL
 ALTER TABLE ACT_PTCPT_TBL ADD EXT_ID VARCHAR(256);
 ALTER TABLE ACT_REL_TBL ADD EXT_ID VARCHAR(256);
 ALTER TABLE ENT_ADDR_TBL ADD EXT_ID VARCHAR(256);
 ALTER TABLE ENT_NAME_TBL ADD EXT_ID VARCHAR(256);
 ALTER TABLE ENT_REL_TBL ADD EXT_ID VARCHAR(256);
 ALTER TABLE PSN_LNG_TBL ADD EXT_ID VARCHAR(256);
 ALTER TABLE PLC_SVC_TBL ADD EXT_ID VARCHAR(256);
 ALTER TABLE ENT_TEL_TBL ADD EXT_ID VARCHAR(256);
 ALTER TABLE SEC_SES_TBL ADD EP VARCHAR(64);--#!
 
 -- Drop column SEC_USR_ID

CREATE TEMPORARY TABLE temp AS
SELECT
  ENT_VRSN_ID
FROM USR_ENT_TBL;

DROP TABLE USR_ENT_TBL;

CREATE TABLE USR_ENT_TBL (
	ENT_VRSN_ID BLOB(16) NOT NULL,
	SEC_USR_ID BLOB(16),
	CONSTRAINT PK_USR_ENT_TBL PRIMARY KEY (ENT_VRSN_ID),
	CONSTRAINT FK_USR_ENT_SEC_USR_ID FOREIGN KEY (SEC_USR_ID) REFERENCES SEC_USR_TBL(USR_ID),
	CONSTRAINT FK_USR_ENT_VRSN_ID FOREIGN KEY (ENT_VRSN_ID) REFERENCES PSN_TBL(ENT_VRSN_ID)
);
CREATE INDEX USR_ENT_SEC_USR_ID_IDX ON USR_ENT_TBL (SEC_USR_ID);

INSERT INTO USR_ENT_TBL
 (ENT_VRSN_ID)
SELECT
  ENT_VRSN_ID
FROM temp;
DROP TABLE temp;
INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20230509-02', UNIXEPOCH(), 'Add external tagging metadata to tables'); 
