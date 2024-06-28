/** 
 * <feature scope="SanteDB.Persistence.Data" id="20240628" name="Update:20240628"  invariantName="sqlite">
 *	<summary>Update: Fixes indexing on the security certificate mapping table to allow for multiple uses of a dsig cert</summary>
 *  <isInstalled>SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE name='SEC_CER_X509_THB_AUT_IDX')</isInstalled>
 * </feature>
 */
DROP INDEX SEC_CER_X509_THB_IDX;
CREATE UNIQUE INDEX SEC_CER_X509_THB_AUT_IDX ON SEC_CER_TBL(X509_THB) WHERE (OBSLT_UTC IS NULL AND CER_USE = 2);
CREATE UNIQUE INDEX SEC_CER_X509_THB_DSIG_IDX ON SEC_CER_TBL(X509_THB) WHERE (OBSLT_UTC IS NULL AND CER_USE = 1 AND USR_ID NOT IN (x'76A0DCFA90366E4AAF9EF1CD68E8C7E8', x'F05968C93C0480448DABF69D6E86696C'));-- ONLY REQUIRED TO BE UNIQUE FOR REGULAR USERS



