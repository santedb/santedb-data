/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230419-01" name="Update:20230419-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="npgsql">
 *	<summary>Update: Adds BI, Match and other system tracking tables to share state between application hosts</summary>
 *	<isInstalled>select ck_patch('20230419-01')</isInstalled>
 * </feature>
 */
 
  CREATE TABLE JOB_STAT_SYSTBL
 (
	JOB_ST_ID UUID NOT NULL DEFAULT UUID_GENERATE_V1(),
	JOB_ID UUID NOT NULL,
	STAT INT NOT NULL,
	LAST_START_UTC TIMESTAMP,
	LAST_STOP_UTC TIMESTAMP,
	CONSTRAINT PK_JOB_STAT_SYSTBL PRIMARY KEY (JOB_ST_ID)
);

CREATE TABLE JOB_SCH_SYSTBL 
(
	JOB_SCH_ID UUID NOT NULL DEFAULT UUID_GENERATE_V1(),
	JOB_ID UUID NOT NULL,
	TYP INT NOT NULL,
	IVL VARCHAR(32),
	START_UTC TIMESTAMP NOT NULL,
	STOP_UTC TIMESTAMP,
	DOW BYTEA,
	CONSTRAINT PK_JOB_SCH_ID PRIMARY KEY (JOB_SCH_ID)
);

CREATE TABLE BI_DEF_SYSTBL 
(
	BI_ID UUID NOT NULL DEFAULT UUID_GENERATE_V1(),
	TYP VARCHAR(256) NOT NULL,
	PUB_ID VARCHAR(256) NOT NULL,
	CONSTRAINT PK_BI_DEF_SYSTBL PRIMARY KEY (BI_ID)
);

CREATE UNIQUE INDEX BI_DEF_PUB_ID_UQ_IDX ON BI_DEF_SYSTBL(PUB_ID);

-- SYSTABLE SEQUENCE
CREATE SEQUENCE SYSTBL_VRSN_SEQ START WITH 1 INCREMENT BY 1;

CREATE TABLE BI_DEF_VRSN_SYSTBL 
(
	BI_ID UUID NOT NULL,
	VRSN_ID UUID NOT NULL DEFAULT UUID_GENERATE_V1(),
	VRSN_SEQ_ID BIGINT UNIQUE NOT NULL DEFAULT NEXTVAL('systbl_vrsn_seq'),
	RPLC_VRSN_ID UUID,
	HEAD BOOLEAN NOT NULL DEFAULT TRUE,
	CRT_PROV_ID UUID NOT NULL,
	CRT_UTC TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	OBSLT_PROV_ID UUID,
	OBSLT_UTC TIMESTAMP,
	STS INTEGER NOT NULL,
	NAME VARCHAR(256) NOT NULL,
	DEF BYTEA NOT NULL,
	CONSTRAINT PK_BI_DEF_VRSN_SYSTBL PRIMARY KEY (VRSN_ID),
	CONSTRAINT FK_BI_DEF_VRSN_BI_DEF FOREIGN KEY (BI_ID) REFERENCES BI_DEF_SYSTBL(BI_ID),
	CONSTRAINT FK_BI_DEF_CRT_PROV_ID FOREIGN KEY (CRT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT FK_BI_DEF_OBSLT_PROV_ID FOREIGN KEY (OBSLT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT FK_BI_DEF_VRSN_RPLC_ID FOREIGN KEY (RPLC_VRSN_ID) REFERENCES BI_DEF_VRSN_SYSTBL(VRSN_ID)
);

CREATE TABLE MATCH_CNF_SYSTBL
(
	MATCH_ID UUID NOT NULL DEFAULT UUID_GENERATE_V1(),
	PUB_ID VARCHAR(128) NOT NULL,
	CONSTRAINT PK_MATCH_CNF PRIMARY KEY (MATCH_ID)
);

CREATE UNIQUE INDEX MATCH_CNF_PUB_ID_UQ_IDX ON MATCH_CNF_SYSTBL(PUB_ID);

CREATE TABLE MATCH_CNF_VRSN_SYSTBL
(
	MATCH_ID UUID NOT NULL,
	VRSN_ID UUID NOT NULL DEFAULT UUID_GENERATE_V1(),
	VRSN_SEQ_ID INTEGER UNIQUE NOT NULL DEFAULT NEXTVAL('systbl_vrsn_seq'),
	RPLC_VRSN_ID UUID,
	HEAD BOOLEAN NOT NULL DEFAULT TRUE,
	CRT_PROV_ID UUID NOT NULL,
	CRT_UTC TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
	OBSLT_PROV_ID UUID,
	OBSLT_UTC TIMESTAMP,
	TRG_TYP VARCHAR(256) NOT NULL,
	STS INTEGER NOT NULL,
	DEF BYTEA NOT NULL,
	CONSTRAINT PK_MATCH_CNF_VRSN_SYSTBL PRIMARY KEY (VRSN_ID),
	CONSTRAINT FK_MATCH_CNF_VRSN_BI_DEF FOREIGN KEY (MATCH_ID) REFERENCES MATCH_CNF_SYSTBL(MATCH_ID),
	CONSTRAINT FK_MATCH_CNF_CRT_PROV_ID FOREIGN KEY (CRT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT FK_MATCH_CNF_OBSLT_PROV_ID FOREIGN KEY (OBSLT_PROV_ID) REFERENCES SEC_PROV_TBL(PROV_ID),
	CONSTRAINT FK_MATCH_CNF_VRSN_RPLC_ID FOREIGN KEY (RPLC_VRSN_ID) REFERENCES MATCH_CNF_VRSN_SYSTBL(VRSN_ID)
);


SELECT REG_PATCH('20230419-01'); 