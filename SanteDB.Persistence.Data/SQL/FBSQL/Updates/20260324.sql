/** 
 * <feature scope="SanteDB.Persistence.Data" id="20260324-01" name="Update:20260324-02"   invariantName="FirebirdSQL" >
 *	<summary>Update: Add sequence to relationships on Act</summary>
 *	<isInstalled>select ck_patch('20260324-02') from rdb$database</isInstalled>
 * </feature>
 */
CREATE SEQUENCE act_rel_seq;--#!
 ALTER TABLE ACT_REL_TBL ADD REL_SEQ_ID BIGINT DEFAULT 0 NOT NULL ;--#!
 
CREATE TRIGGER TG_ACT_REL_TBL_SEQ FOR ACT_REL_TBL ACTIVE BEFORE INSERT POSITION 0 AS BEGIN
	NEW.REL_SEQ_ID = NEXT VALUE FOR act_rel_seq;
END;--#!
 SELECT REG_PATCH('20260330-02') FROM RDB$DATABASE;