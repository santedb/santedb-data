/** 
 * <feature scope="SanteDB.Persistence.Data" id="20240605" name="Update:20240605"  invariantName="sqlite">
 *	<summary>Update: Adds the identifier classification code column</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE name='ID_DMN_TBL' AND sql LIKE '%cls_cd_id%')</isInstalled>
 * </feature>
 */
ALTER TABLE ID_DMN_TBL ADD CLS_CD_ID BLOB(16);