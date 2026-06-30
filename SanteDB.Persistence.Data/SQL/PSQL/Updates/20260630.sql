/** 
 * <feature scope="SanteDB.Persistence.Data" id="20260630-01" name="Update:20260630-01"   invariantName="npgsql" >
 *	<summary>Update: Extends SMK ALE Table</summary>
 *	<isInstalled>select ck_patch('20260630-01')</isInstalled>
 *	<initializer>SanteDB.Persistence.Data.Migration.MigrateAleConfiguration, SanteDB.Persistence.Data</initializer>
 * </feature>
 */

 ALTER TABLE ALE_SYSTBL ADD X5T VARCHAR(64);

 DROP FUNCTION IF EXISTS SET_ALE_SMK;
 
CREATE OR REPLACE FUNCTION SET_ALE_SMK(NEW_ALE_SMK_IN BYTEA, X5T_IN VARCHAR(64)) RETURNS VOID AS 
$$
BEGIN
	DELETE FROM ALE_SYSTBL;
	INSERT INTO ALE_SYSTBL (ALE_SMK, X5T) VALUES (NEW_ALE_SMK_IN, X5T_IN);
END
$$ LANGUAGE PLPGSQL;

CREATE OR REPLACE FUNCTION GET_ALE_X5T() RETURNS VARCHAR(64) AS 
$$
BEGIN
	RETURN (SELECT X5T FROM ALE_SYSTBL LIMIT 1);
END
$$ LANGUAGE PLPGSQL;


 SELECT REG_PATCH('20260630-01');