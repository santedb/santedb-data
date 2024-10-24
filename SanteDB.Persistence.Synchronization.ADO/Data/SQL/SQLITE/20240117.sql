﻿/** 
 * <feature scope="SanteDB.Persistence.Synchronization.ADO" id="20240117-00" name="20240117-01" invariantName="sqlite">
 *	<summary>Installs tracking of synchronization queue metadata in primary database</summary>
 *	<remarks>This script registers a synchronization queue table which allows limited metadata about the synchronization queues to be read/queried</remarks>
 *  <isInstalled mustSucceed="true">SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type='table' AND name='SYNC_Q_SYSTBL')</isInstalled>
 * </feature>
 */
 
 ALTER TABLE SYNC_LOG_TBL RENAME TO SYNC_LOG_SYSTBL;
 
 CREATE TABLE SYNC_Q_SYSTBL (
	Q_ID BLOB(16) DEFAULT (RANDOMBLOB(16)) NOT NULL,
	Q_NAME VARCHAR(10) NOT NULL,
	Q_PAT INT NOT NULL,
	CONSTRAINT PK_SYNC_Q_SYSTBL PRIMARY KEY (Q_ID)
 ); 
  
 CREATE TABLE SYNC_Q_ENT_SYSTBL (
	SEQ_ID INTEGER PRIMARY KEY,
	COR_ID BLOB(16) DEFAULT (RANDOMBLOB(16)) NOT NULL,
	CRT_UTC BIGINT DEFAULT (UNIXEPOCH()) NOT NULL,
	Q_ID BLOB(16) NOT NULL,
	TYP VARCHAR(64) NOT NULL,
	DF BLOB(16) NOT NULL,
	CTY VARCHAR(256) NOT NULL,
	OP INTEGER NOT NULL CHECK(OP IN (0, 1, 2, 4)), -- OPERATION
	RETRY INTEGER,
	CONSTRAINT FK_SYNC_Q_ENT_Q_SYSTBL FOREIGN KEY (Q_ID) REFERENCES SYNC_Q_SYSTBL(Q_ID)
 );

 
 CREATE TABLE SYNC_Q_ENT_DL_SYSTBL (
	SEQ_ID INTEGER NOT NULL,
	ORIG_Q BLOB(16) NOT NULL,
	RSN TEXT NOT NULL,
	CONSTRAINT PK_SYNC_Q_ENT_DL_SYSTBL PRIMARY KEY (SEQ_ID),
	CONSTRAINT FK_SYNC_Q_ENT_DL_ENT_SYSTBL FOREIGN KEY (SEQ_ID) REFERENCES SYNC_Q_ENT_SYSTBL(SEQ_ID),
	CONSTRAINT FK_SYNC_Q_ENT_DL_ORIG_SYSTBL FOREIGN KEY (ORIG_Q) REFERENCES SYNC_Q_SYSTBL(Q_ID)
 );

INSERT INTO SYNC_Q_SYSTBL (Q_NAME, Q_PAT) VALUES ('in', 1); 
 INSERT INTO SYNC_Q_SYSTBL (Q_NAME, Q_PAT) VALUES ('out', 2);
 INSERT INTO SYNC_Q_SYSTBL (Q_NAME, Q_PAT) VALUES ('admin', 10);
 INSERT INTO SYNC_Q_SYSTBL (Q_NAME, Q_PAT) VALUES ('deadletter', 132);