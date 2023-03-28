/** 
 * <feature scope="SanteDB.Persistence.Data" id="20230328-01" name="Update:20230328-01" applyRange="1.1.0.0-1.2.0.0"  invariantName="npgsql">
 *	<summary>Update: Make refresh tokens nullable in the database.</summary>
 *	<isInstalled>select ck_patch('20230328-01')</isInstalled>
 * </feature>
 */
 
 ALTER TABLE "sec_ses_tbl"
	ALTER COLUMN "rfrsh_tkn" DROP NOT NULL,
	ALTER COLUMN "rfrsh_tkn" DROP DEFAULT,
	ALTER COLUMN "rfrsh_exp_utc" DROP NOT NULL,
	ALTER COLUMN "rfrsh_exp_utc" DROP DEFAULT;

SELECT REG_PATCH('20230328-01'); 