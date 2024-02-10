/** 
 * <feature scope="SanteDB.Persistence.Data" id="20240210" name="Update:Removes unique constraint from the versioning tables for clients (allowing for sync)" applyRange="0.2.0.0-0.9.0.0" environment="Client Gateway Test" invariantName="sqlite">
 *	<summary>Update:Add relationship validation for server environment</summary>
 *  <isInstalled>SELECT NOT EXISTS (SELECT 1 FROM sqlite_master WHERE name='ACT_VRSN_SEQ_UQ_IDEX')</isInstalled>
 * </feature>
 */
DROP INDEX CD_VRSN_SEQ_ID_UQ_IDX;
DROP INDEX ENT_VRSN_SEQ_ID_UQ_IDX;
DROP INDEX ACT_VRSN_SEQ_ID_UQ_IDX;