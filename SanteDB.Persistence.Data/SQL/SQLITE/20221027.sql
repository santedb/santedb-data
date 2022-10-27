/** 
 * <feature scope="SanteDB.Persistence.Data" id="20221027" name="Update:Session User Index" applyRange="0.2.0.0-0.9.0.0" invariantName="sqlite">
 *	<summary>Update:Adds index for user key in session table</summary>
 *	<remarks>Adds an index for the </remarks>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type='index' AND name='SEC_SES_USR_ID_IDX')</isInstalled>
 * </feature>
 */
 -- OPTIONAL

 CREATE INDEX IF NOT EXISTS SEC_SES_USR_ID_IDX ON SEC_SES_TBL(USR_ID); --#!