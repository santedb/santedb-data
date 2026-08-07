/** 
 * <feature scope="SanteDB.Persistence.Data" id="20260806-01" name="Update:20260720-01"   invariantName="npgsql" >
 *	<summary>Update: Remove constraints for VRSN SEQ ID</summary>
 *	<isInstalled>select ck_patch('20260806-01')</isInstalled>
 * </feature>
 */

 -- DROP THE EFFT_VRSN_SEQ FKS 
 DROP INDEX IF EXISTS act_vrsn_vrsn_seq_id_idx CASCADE;
 DROP INDEX IF EXISTS ent_vrsn_vrsn_seq_id_idx CASCADE;
 DROP INDEX IF EXISTS cd_vrsn_vrsn_seq_id_idx CASCADE;
 DROP INDEX IF EXISTS cd_vrsn_vrsn_seq_idx CASCADE;
 
 -- REESTABLISH UQ KEYS
 CREATE UNIQUE INDEX act_vrsn_vrsn_seq_id_idx ON act_vrsn_tbl (vrsn_seq_id);
 CREATE UNIQUE INDEX ent_vrsn_vrsn_seq_id_idx ON ent_vrsn_tbl (vrsn_seq_id);
 CREATE UNIQUE INDEX cd_vrsn_vrsn_seq_id_idx ON cd_vrsn_tbl (vrsn_seq_id);

 SELECT REG_PATCH('20260806-01');