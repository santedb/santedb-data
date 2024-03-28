/** 
 * <feature scope="SanteDB.Persistence.Data" id="20240210" name="Update:Removes unique constraint from the versioning tables for clients (allowing for sync)"  environment="Client Gateway Debugger" invariantName="sqlite">
 *	<summary>Update:Add relationship validation for server environment</summary>
 *  <isInstalled>SELECT NOT EXISTS (SELECT 1 FROM sqlite_master WHERE name='CD_VRSN_SEQ_ID_UQ_IDX')</isInstalled>
 * </feature>
 */
DROP INDEX CD_VRSN_SEQ_ID_UQ_IDX;
DROP INDEX ENT_VRSN_SEQ_ID_UQ_IDX;
DROP INDEX ACT_VRSN_SEQ_ID_UQ_IDX;