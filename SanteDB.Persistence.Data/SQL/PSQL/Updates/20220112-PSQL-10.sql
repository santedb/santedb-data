/** 
 * <feature scope="SanteDB.Persistence.Data" id="20220112-04" name="Update:20220112-04" required="false" applyRange="1.1.0.0-1.2.0.0"  invariantName="npgsql">
 *	<summary>Special patch for PostgreSQL 10 and lower</summary>
 *	<isInstalled>select ck_patch('20220112-04')</isInstalled>
 *  <canInstall>SELECT TRUE FROM (SELECT version() v) i WHERE (v ILIKE '%PostgreSQL 9.%' OR v ILIKE '%PostgreSQL 10.%')</canInstall>
 * </feature>
 */
 
CREATE OR REPLACE FUNCTION fti_tsquery(search_term_in in text)
RETURNS tsquery 
IMMUTABLE
AS 
$$
BEGIN
	RETURN TO_TSQUERY(ARRAY_TO_STRING(STRING_TO_ARRAY(search_term_in, ' '), ' & '));
END;
$$ LANGUAGE plpgsql;
SELECT REG_PATCH('20220112-04');
