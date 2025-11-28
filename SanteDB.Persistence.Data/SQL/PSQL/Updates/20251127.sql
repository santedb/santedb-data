/** 
 * <feature scope="SanteDB.Persistence.Data" id="20251127-01" name="Update:20251127-01"   invariantName="npgsql" >
 *	<summary>Update: Adds default value constraint to tables missing them</summary>
 *	<isInstalled>select ck_patch('20251127-01')</isInstalled>
 * </feature>
 */
 ALTER TABLE SEC_APP_CLM_TBL ALTER COLUMN CLM_ID SET DEFAULT uuid_generate_v1();
 ALTER TABLE SEC_DEV_CLM_TBL ALTER COLUMN CLM_ID SET DEFAULT uuid_generate_v1();
 SELECT REG_PATCH('20251127-01');