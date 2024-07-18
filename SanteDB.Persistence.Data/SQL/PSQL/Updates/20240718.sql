/** 
 * <feature scope="SanteDB.Persistence.Data" id="20240718-01" name="Update:20240718-01" invariantName="npgsql">
 *	<summary>Update: Fixes the entity relationship trigger and adds new relationship validations</summary>
 *	<isInstalled>select ck_patch('20240718-01')</isInstalled>
 * </feature>
 */

CREATE TRIGGER ENT_REL_TBL_VRFY BEFORE INSERT OR UPDATE ON
    ENT_REL_TBL FOR EACH ROW EXECUTE PROCEDURE TRG_VRFY_ENT_REL_TBL(); --#!

INSERT INTO REL_VRFY_SYSTBL (REL_TYP_CD_ID, SRC_CLS_CD_ID, TRG_CLS_CD_ID, ERR_DESC) 
    VALUES ('4F6273D3-8223-4E18-8596-C718AD029DEB','ff34dfa7-c6d3-4f8b-bc9f-14bcdc13ba6c','b76ff324-b174-40b7-a6ac-d1fdf8e23967', 'ServiceDeliveryLocation=[LocatedEntity]=>Container') ON CONFLICT DO NOTHING;
INSERT INTO REL_VRFY_SYSTBL (REL_TYP_CD_ID, SRC_CLS_CD_ID, TRG_CLS_CD_ID, ERR_DESC) 
    VALUES ('9C02A621-8565-46B4-94FF-A2BD210989B1','ff34dfa7-c6d3-4f8b-bc9f-14bcdc13ba6c','b76ff324-b174-40b7-a6ac-d1fdf8e23967', 'ServiceDeliveryLocation=[HeldEntity]=>Container') ON CONFLICT DO NOTHING;
INSERT INTO REL_VRFY_SYSTBL (REL_TYP_CD_ID, SRC_CLS_CD_ID, TRG_CLS_CD_ID, ERR_DESC) 
    VALUES ('9C02A621-8565-46B4-94FF-A2BD210989B1','b76ff324-b174-40b7-a6ac-d1fdf8e23967','fafec286-89d5-420b-9085-054aca9d1eef', 'Container=[HeldEntity]=>ManufacturedMaterial') ON CONFLICT DO NOTHING;
INSERT INTO REL_VRFY_SYSTBL (REL_TYP_CD_ID, SRC_CLS_CD_ID, TRG_CLS_CD_ID, ERR_DESC) 
    VALUES ('77B6D8CD-05A0-4B1F-9E14-B895203BF40C','6a2b00ba-501b-4523-b57c-f96d8ae44684','ff34dfa7-c6d3-4f8b-bc9f-14bcdc13ba6c', 'UserEntity=[MaintainedEntity]=>ServiceDeliveryLocation') ON CONFLICT DO NOTHING;
INSERT INTO REL_VRFY_SYSTBL (REL_TYP_CD_ID, SRC_CLS_CD_ID, TRG_CLS_CD_ID, ERR_DESC) 
    VALUES ('77B6D8CD-05A0-4B1F-9E14-B895203BF40C','7c08bd55-4d42-49cd-92f8-6388d6c4183f','ff34dfa7-c6d3-4f8b-bc9f-14bcdc13ba6c', 'Organization=[MaintainedEntity]=>ServiceDeliveryLocation') ON CONFLICT DO NOTHING;

 SELECT REG_PATCH('20240718-01'); 
