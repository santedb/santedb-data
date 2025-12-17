/** 
 * <feature scope="SanteDB.Persistence.Data" id="20251216-01" name="Update:20251216-01"   invariantName="npgsql" >
 *	<summary>Update: Add not before and not after bounds to act protocol</summary>
 *	<isInstalled>select ck_patch('20251216-01')</isInstalled>
 * </feature>
 */
 
 ALTER TABLE ACT_PROTO_ASSOC_TBL ADD NAF DATE;
 ALTER TABLE ACT_PROTO_ASSOC_TBL ADD NBF DATE;
SELECT REG_PATCH('20251216-01');