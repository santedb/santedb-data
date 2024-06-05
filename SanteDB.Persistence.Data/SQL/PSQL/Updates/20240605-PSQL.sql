/** 
 * <feature scope="SanteDB.Persistence.Data" id="20240605-01" name="Update:20240605-01" invariantName="npgsql">
 *	<summary>Update: Adds identity domain scope to the identity domain registration table</summary>
 *	<isInstalled>select ck_patch('20240605-01')</isInstalled>
 * </feature>
 */
ALTER TABLE ID_DMN_TBL ADD CLS_CD_ID UUID;
ALTER TABLE ID_DMN_TBL ADD CONSTRAINT CK_ID_DMN_CLS_CD CHECK (CLS_CD_ID IS NULL OR CK_IS_CD_SET_MEM(CLS_CD_ID, 'IdentifierType', FALSE));
 SELECT REG_PATCH('20240605-01'); 
