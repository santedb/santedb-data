/** 
 * <feature scope="SanteDB.Persistence.Data" id="20241022-02" name="Update:20241022-02" invariantName="npgsql">
 *	<summary>Update: Recreates the obsoletion reason</summary>
 *	<isInstalled>select ck_patch('20241022-02')</isInstalled>
 * </feature>
 */
 -- OPTIONAL
 ALTER TABLE ACT_VRSN_TBL DROP OBSLT_RSN;--#!
 -- OPTIONAL
 ALTER TABLE ACT_VRSN_TBL ADD OBSLT_RSN UUID;--#!
 -- OPTIONAL
 CREATE INDEX ent_tel_use_idx ON ent_tel_tbl(use_cd_id); --#!
 SELECT REG_PATCH('20241022-02'); 
