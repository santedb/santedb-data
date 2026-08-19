/** 
 * <feature scope="SanteDB.Persistence.Data" id="20260629-01" name="Update:20260629-01"   invariantName="npgsql" >
 *	<summary>Update: Extends claim values</summary>
 *	<isInstalled>select ck_patch('20260629-01')</isInstalled>
 * </feature>
 */
 
 ALTER TABLE sec_usr_clm_tbl ALTER clm_val TYPE VARCHAR(512);
 ALTER TABLE sec_app_clm_tbl ALTER clm_val TYPE VARCHAR(512);
 ALTER TABLE sec_dev_clm_tbl ALTER clm_val TYPE VARCHAR(512);

 SELECT REG_PATCH('20260629-01');