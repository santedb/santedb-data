/** 
 * <feature scope="SanteDB.Persistence.Data" id="20260324-01" name="Update:20260324-02"   invariantName="npgsql" environment="Server">
 *	<summary>Update: Add sequence to relationships on Act</summary>
 *	<isInstalled>select ck_patch('20260324-02')</isInstalled>
 * </feature>
 */
 CREATE SEQUENCE act_rel_seq START WITH 1 INCREMENT BY 1;
 ALTER TABLE ACT_REL_TBL ADD REL_SEQ_ID BIGINT NOT NULL DEFAULT nextval('act_rel_seq');
 SELECT REG_PATCH('20260330-02');