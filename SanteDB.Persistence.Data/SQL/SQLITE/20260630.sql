/** 
 * <feature scope="SanteDB.Persistence.Data" id="20260630-01" name="Update:20260630-01"   invariantName="sqlite"  >
 *	<summary>Update: Add additional ALE fields</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM patch_db_systbl WHERE patch_id='20260630-01')</isInstalled>
 *	<initializer>SanteDB.Persistence.Data.Migration.MigrateAleConfiguration, SanteDB.Persistence.Data</initializer>
 * </feature>
 */

ALTER TABLE ALE_SYSTBL ADD X5T VARCHAR(32);
ALTER TABLE ALE_SYSTBL ADD STORE VARCHAR(32);
INSERT INTO PATCH_DB_SYSTBL (PATCH_ID, APPLY_DATE, INFO_NAME) VALUES ('20260630-01', UNIXEPOCH(), 'Add additional ALE fields');--#!
