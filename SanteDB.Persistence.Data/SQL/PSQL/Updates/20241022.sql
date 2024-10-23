/** 
 * <feature scope="SanteDB.Persistence.Data" id="20241022-01" name="Update:20241022-01" invariantName="npgsql">
 *	<summary>Update: Recreates the obsoletion reason</summary>
 *	<isInstalled>select ck_patch('20241022-01')</isInstalled>
 * </feature>
 */
 ALTER TABLE ACT_VRSN_TBL DROP OBSLT_RSN;
 ALTER TABLE ACT_VRSN_TBL ADD OBSLT_RSN UUID;
 SELECT REG_PATCH('20241022-01'); 
