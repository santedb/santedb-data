/** 
 * <feature scope="SanteDB.Persistence.Data" id="20240718-01" name="Update:20240718-01" invariantName="npgsql">
 *	<summary>Update: Fixes the entity relationship trigger and adds new relationship validations</summary>
 *	<isInstalled>select ck_patch('20240718-01')</isInstalled>
 * </feature>
 */

CREATE TRIGGER ENT_REL_TBL_VRFY BEFORE INSERT OR UPDATE ON
    ENT_REL_TBL FOR EACH ROW EXECUTE PROCEDURE TRG_VRFY_ENT_REL_TBL(); --#!
CREATE INDEX IF NOT EXISTS ACT_VRSN_TYP_CD_IDX ON ACT_VRSN_TBL(TYP_CD_ID) WHERE (HEAD); --#!
 SELECT REG_PATCH('20240718-01'); 
