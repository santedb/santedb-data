/** 
 * <feature scope="SanteDB.Persistence.Data" id="20231116-01" name="Update:20231116-01"   invariantName="sqlite">
 *	<summary>Update: Adds CDSS storage to the primary database</summary>
 *	<isInstalled>select count(*) > 0 from patch_db_systbl WHERE PATCH_ID = '20231116-01'</isInstalled>
 * </feature>
 */
 CREATE TABLE CP_TBL 
 (
	ACT_VRSN_ID BLOB(16) NOT NULL,
	TITLE TEXT, 
	PROG VARCHAR(256),
	CONSTRAINT PK_CP_TBL PRIMARY KEY (ACT_VRSN_ID),
	CONSTRAINT FK_CP_ACT_VRSN_TBL FOREIGN KEY (ACT_VRSN_ID) REFERENCES ACT_VRSN_TBL(ACT_VRSN_ID)
 );

 -- CDSS LIBRARY STORAGE 
 CREATE TABLE CDSS_LIB_TBL (
	LIB_ID BLOB(16) DEFAULT (RANDOMBLOB(16)) NOT NULL,
	CLS VARCHAR(512) NOT NULL,
	IS_SYS BOOLEAN NOT NULL DEFAULT FALSE,
	CONSTRAINT PK_CDSS_LIB_TBL PRIMARY KEY (LIB_ID)
);

CREATE TABLE CDSS_LIB_VRSN_TBL (
	LIB_VRSN_ID BLOB(16) DEFAULT (RANDOMBLOB(16)) NOT NULL,
	LIB_ID UUID NOT NULL,
	VRSN_SEQ_ID INTEGER UNIQUE,
	RPLC_VRSN_ID BLOB(16),
	HEAD BOOL NOT NULL DEFAULT TRUE,
	CRT_PROV_ID BLOB(16) NOT NULL,
	CRT_UTC BIGINT DEFAULT (UNIXEPOCH()) NOT NULL,
	OBSLT_PROV_ID BLOB(16),
	OBSLT_UTC BIGINT,
	ID VARCHAR(256) NOT NULL,
	NAME VARCHAR(512) NOT NULL,
	VER VARCHAR(256),
	OID VARCHAR(256),
	DOC TEXT,
	DEF BLOB NOT NULL,
	CONSTRAINT PK_CDSS_LIB_VRSN_TBL PRIMARY KEY (LIB_VRSN_ID),
	CONSTRAINT FK_CDSS_LIB_LIB_TBL FOREIGN KEY (LIB_ID) REFERENCES CDSS_LIB_TBL(LIB_ID),
	CONSTRAINT FK_CDSS_LIB_VRSN_CRT_PROV_TBL FOREIGN KEY (CRT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT FK_CDSS_LIB_VRSN_OBSLT_PROV_TBL FOREIGN KEY (OBSLT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT FK_CDSS_LIB_VRSN_RPLC_TBL FOREIGN KEY (RPLC_VRSN_ID) REFERENCES CDSS_LIB_VRSN_TBL(LIB_VRSN_ID),
	CONSTRAINT FK_OBSLT_PROV_LOGIC CHECK ((OBSLT_PROV_ID IS NULL AND OBSLT_UTC IS NULL) OR (OBSLT_PROV_ID IS NOT NULL AND OBSLT_UTC IS NOT NULL))
);


-- HACK FOR SQLITE
CREATE TRIGGER TRG_CDSS_LIB_VRSN_TBL_AU AFTER INSERT ON CDSS_LIB_VRSN_TBL 
	FOR EACH ROW 
	WHEN NEW.VRSN_SEQ_ID IS NULL	BEGIN
		UPDATE CDSS_LIB_VRSN_TBL SET VRSN_SEQ_ID = ROWID WHERE LIB_VRSN_ID = NEW.LIB_VRSN_ID;
	END;

CREATE UNIQUE INDEX CDSS_LIB_ID_IDX ON CDSS_LIB_VRSN_TBL(ID) WHERE (OBSLT_UTC IS NULL);
CREATE UNIQUE INDEX CDSS_LIB_NAME_IDX ON CDSS_LIB_VRSN_TBL(NAME) WHERE (OBSLT_UTC IS NULL);
CREATE INDEX CDSS_LIB_NAM_IDX ON CDSS_LIB_VRSN_TBL(NAME);

INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20231116-01', UNIXEPOCH(), 'Adds CDSS storage tables to database'); 
