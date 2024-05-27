/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230105" name="Update:Cumulative Update for SQLite"  invariantName="sqlite">
 *	<summary>Update:Updates the foreign data staging table to add description</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE tbl_name = 'FD_STG_SYSTBL' AND sql LIKE '%DESCR%')</isInstalled>
 * </feature>
 */
ALTER TABLE FD_STG_SYSTBL ADD DESCR VARCHAR(512);
DROP INDEX PROTO_NAME_UQ_IDX ;
CREATE UNIQUE INDEX PROTO_OID_UQ_IDX ON PROTO_TBL(OID);-- WHEN (OBSLT_UTC IS NULL);

