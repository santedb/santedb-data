﻿/** 
 * <feature scope="SanteDB.Persistence.Data" id="20210512-01" name="Update:20210512-01"   invariantName="npgsql">
 *	<summary>Update: Adds confidence codes for relationships</summary>
 *	<isInstalled>select ck_patch('20210512-01')</isInstalled>
 * </feature>
 */
ALTER TABLE ent_rel_tbl ADD conf REAL;
SELECT REG_PATCH('20210512-01');
