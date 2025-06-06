/** 
 * <feature scope="SanteDB.Persistence.Data" id="20231116-01" name="Update:20231116-01"   invariantName="npgsql">
 *	<summary>Update: Updates the storage of care plans and CDSS library logic</summary>
 *	<isInstalled>select ck_patch('20231116-01')</isInstalled>
 * </feature>
 */
 CREATE TABLE CP_TBL 
 (
	ACT_VRSN_ID UUID NOT NULL,
	TITLE TEXT, 
	PROG VARCHAR(256),
	CONSTRAINT PK_CP_TBL PRIMARY KEY (ACT_VRSN_ID),
	CONSTRAINT FK_CP_ACT_VRSN_TBL FOREIGN KEY (ACT_VRSN_ID) REFERENCES ACT_VRSN_TBL(ACT_VRSN_ID)
 );

 -- CDSS LIBRARY STORAGE 
 CREATE TABLE CDSS_LIB_TBL (
	LIB_ID UUID NOT NULL DEFAULT UUID_GENERATE_V1(),
	CLS VARCHAR(512) NOT NULL,
	IS_SYS BOOLEAN NOT NULL DEFAULT FALSE,
	CONSTRAINT PK_CDSS_LIB_TBL PRIMARY KEY (LIB_ID)
);

CREATE SEQUENCE CDSS_LIB_VRSN_SEQ START WITH 1 INCREMENT BY 1;

CREATE TABLE CDSS_LIB_VRSN_TBL (
	LIB_VRSN_ID UUID NOT NULL DEFAULT UUID_GENERATE_V1(),
	LIB_ID UUID NOT NULL,
	VRSN_SEQ_ID BIGINT NOT NULL DEFAULT nextval('CDSS_LIB_VRSN_SEQ'),
	RPLC_VRSN_ID UUID,
	HEAD BOOLEAN NOT NULL DEFAULT TRUE,
	CRT_PROV_ID UUID NOT NULL,
	CRT_UTC TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
	OBSLT_PROV_ID UUID,
	OBSLT_UTC TIMESTAMPTZ,
	ID VARCHAR(256) NOT NULL,
	NAME VARCHAR(512) NOT NULL,
	VER VARCHAR(256),
	OID VARCHAR(256),
	DOC TEXT,
	DEF BYTEA NOT NULL,
	CONSTRAINT PK_CDSS_LIB_VRSN_TBL PRIMARY KEY (LIB_VRSN_ID),
	CONSTRAINT FK_CDSS_LIB_LIB_TBL FOREIGN KEY (LIB_ID) REFERENCES CDSS_LIB_TBL(LIB_ID),
	CONSTRAINT FK_CDSS_LIB_VRSN_CRT_PROV_TBL FOREIGN KEY (CRT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT FK_CDSS_LIB_VRSN_OBSLT_PROV_TBL FOREIGN KEY (OBSLT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT FK_CDSS_LIB_VRSN_RPLC_TBL FOREIGN KEY (RPLC_VRSN_ID) REFERENCES CDSS_LIB_VRSN_TBL(LIB_VRSN_ID),
	CONSTRAINT CK_OBSLT_PROV_LOGIC CHECK ((OBSLT_PROV_ID IS NULL AND OBSLT_UTC IS NULL) OR (OBSLT_PROV_ID IS NOT NULL AND OBSLT_UTC IS NOT NULL))
);

CREATE UNIQUE INDEX CDSS_LIB_ID_IDX ON CDSS_LIB_VRSN_TBL(ID) WHERE (OBSLT_UTC IS NULL);
CREATE UNIQUE INDEX CDSS_LIB_NAME_IDX ON CDSS_LIB_VRSN_TBL(NAME) WHERE (OBSLT_UTC IS NULL);
CREATE INDEX CDSS_LIB_NAM_IDX ON CDSS_LIB_VRSN_TBL(NAME);


SELECT REG_PATCH('20231116-01'); 
