/** 
 * <feature scope="SanteDB.Persistence.Data" id="20260817-01" name="Update:20260817-01"   invariantName="npgsql" >
 *	<summary>Update: Extends Act and Entity Tag Names</summary>
 *	<isInstalled>select ck_patch('20260817-01')</isInstalled>
 * </feature>
 */
 
 ALTER TABLE act_tag_tbl ALTER tag_name TYPE VARCHAR(256);
 ALTER TABLE ent_tag_tbl ALTER tag_name TYPE VARCHAR(256);

 SELECT REG_PATCH('20260817-01');