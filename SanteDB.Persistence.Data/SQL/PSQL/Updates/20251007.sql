/** 
 * <feature scope="SanteDB.Persistence.Data" id="20251007-03" name="Update:20251007-01"   invariantName="npgsql" >
 *	<summary>Update: Makes values optional on observations and extends the language code to three characters</summary>
 *	<isInstalled>select ck_patch('20251007-03')</isInstalled>
 * </feature>
 */
 ALTER TABLE CD_OBS_TBL ALTER COLUMN VAL_CD_ID DROP NOT NULL;
 ALTER TABLE TXT_OBS_TBL ALTER COLUMN OBS_VAL DROP NOT NULL;
 ALTER TABLE QTY_OBS_TBL ALTER COLUMN QTY DROP NOT NULL;
 ALTER TABLE CD_NAME_TBL ALTER COLUMN LANG_CS TYPE VARCHAR(3);
 SELECT REG_PATCH('20251007-03');