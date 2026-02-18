/** 
 * <feature scope="SanteDB.Persistence.PubSub.ADO" id="20210311-01" name="Update:20210311-01"   invariantName="sqlite">
 *	<summary>Update: Installs the Pub/Sub ADO Tables</summary>
 *	<remarks>This table is used to register channels and subscriptions</remarks>
 *  <isInstalled mustSucceed="true">SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type='table' AND name='SUB_CHNL_TBL')</isInstalled>
 * </feature>
 */


-- SUBSCRIPTION LOG
CREATE TABLE SUB_LOG_TBL (
	SUB_LOG_ID BLOB(16) NOT NULL DEFAULT (RANDOMBLOB(16)) ,
	SUB_ID BLOB(16) NOT NULL,
	OBJ_ID BLOB(16) NOT NULL,
	VRSN_SEQ_ID BIGINT NOT NULL,
	DSPTCH_UTC BIGINT NOT NULL DEFAULT (UNIXEPOCH()),
	OUTC INT NOT NULL,
	EVT INT NOT NULL,
	CONSTRAINT PK_SUB_LOG_TBL PRIMARY KEY (SUB_LOG_ID),
	CONSTRAINT FK_SUB_LOG_SUB_TBL FOREIGN KEY (SUB_ID) REFERENCES SUB_TBL(SUB_ID)
);


CREATE INDEX IX_SUB_LOG_OBJ ON SUB_LOG_TBL(OBJ_ID);
