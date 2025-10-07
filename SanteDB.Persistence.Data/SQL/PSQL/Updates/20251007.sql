/** 
 * <feature scope="SanteDB.Persistence.Data" id="20251007-01" name="Update:20251007-01"   invariantName="npgsql" >
 *	<summary>Update: Makes values optional on observations</summary>
 *	<isInstalled>select ck_patch('20251007-01')</isInstalled>
 * </feature>
 */
 ALTER TABLE CD_OBS_TBL ALTER COLUMN VAL_CD_ID DROP NOT NULL;
 ALTER TABLE TXT_OBS_TBL ALTER COLUMN OBS_VAL DROP NOT NULL;
 ALTER TABLE QTY_OBS_TBL ALTER COLUMN QTY DROP NOT NULL;
 SELECT REG_PATCH('20251007-01');