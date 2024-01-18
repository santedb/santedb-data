/** 
 * <feature scope="SanteDB.Persistence.Synchronization.ADO" id="20240117-00" name="20240117-01" invariantName="npgsql">
 *	<summary>Installs tracking of synchronization queue metadata in primary database</summary>
 *	<remarks>This script registers a synchronization queue table which allows limited metadata about the synchronization queues to be read/queried</remarks>
 *  <isInstalled mustSucceed="true">SELECT to_regclass('public.sync_q_systbl') IS NOT NULL;</isInstalled>
 * </feature>
 */

 
 ALTER TABLE SYNC_LOG_TBL RENAME TO SYNC_LOG_SYSTBL;

 CREATE TABLE SYNC_Q_SYSTBL (
	Q_ID UUID NOT NULL DEFAULT UUID_GENERATE_V1(),
	Q_NAME VARCHAR(10) NOT NULL,
	Q_PAT INT NOT NULL,
	CONSTRAINT PK_SYNC_Q_SYSTBL PRIMARY KEY (Q_ID)
 ); 

 CREATE SEQUENCE SYNC_Q_ENT_SEQ START WITH 1 INCREMENT BY 1;

 CREATE TABLE SYNC_Q_ENT_SYSTBL (
	SEQ_ID INTEGER NOT NULL DEFAULT NEXTVAL('sync_q_ent_seq'),
	COR_ID UUID NOT NULL DEFAULT UUID_GENERATE_V4(),
	CRT_UTC TIMESTAMPTZ NOT NULL DEFAULT CURRENT_TIMESTAMP,
	Q_ID UUID NOT NULL,
	TYP VARCHAR(64) NOT NULL,
	DF UUID NOT NULL,
	CTY VARCHAR(256) NOT NULL,
	OP INTEGER NOT NULL CHECK(OP IN (0, 1, 2, 4)), -- OPERATION
	RETRY INTEGER,
	CONSTRAINT PK_SYNC_Q_ENT_SYSTBL PRIMARY KEY (SEQ_ID),
	CONSTRAINT FK_SYNC_Q_ENT_Q_SYSTBL FOREIGN KEY (Q_ID) REFERENCES SYNC_Q_SYSTBL(Q_ID)
 );

 CREATE TABLE SYNC_Q_ENT_DL_SYSTBL (
	SEQ_ID INTEGER NOT NULL,
	ORIG_Q UUID NOT NULL,
	RSN TEXT NOT NULL,
	CONSTRAINT PK_SYNC_Q_ENT_DL_SYSTBL PRIMARY KEY (SEQ_ID),
	CONSTRAINT FK_SYNC_Q_ENT_DL_ENT_SYSTBL FOREIGN KEY (SEQ_ID) REFERENCES SYNC_Q_ENT_SYSTBL(SEQ_ID),
	CONSTRAINT FK_SYNC_Q_ENT_DL_ORIG_SYSTBL FOREIGN KEY (ORIG_Q) REFERENCES SYNC_Q_SYSTBL(Q_ID)
 );

 INSERT INTO sync_q_systbl (q_name, q_pat) VALUES ('in', 1); 
 INSERT INTO sync_q_systbl (q_name, q_pat) VALUES ('out', 2);
 INSERT INTO sync_q_systbl (q_name, q_pat) VALUES ('admin', 10);
 INSERT INTO sync_q_systbl (q_name, q_pat) VALUES ('deadletter', 132);