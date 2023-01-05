/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230105" name="Update:Cumulative Update for SQLite" applyRange="0.2.0.0-0.9.0.0" invariantName="sqlite">
 *	<summary>Update:Cumulative update for SQLite</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE tbl_name = 'FD_STG_SYSTBL' AND sql LIKE '%DESCR%')</isInstalled>
 * </feature>
 */
ALTER TABLE FD_STG_SYSTBL ADD DESCR VARCHAR(512);
