/** 
 * <feature scope="SanteDB.Persistence.Data" id="20260720-01" name="Update:20260720-01"   invariantName="npgsql" >
 *	<summary>Update: Updates the relationships for family members</summary>
 *	<isInstalled>select ck_patch('20260720-01')</isInstalled>
 *	<initializer>SanteDB.Persistence.Data.Migration.MigrateAleConfiguration, SanteDB.Persistence.Data</initializer>
 * </feature>
 */


INSERT INTO rel_vrfy_systbl (rel_typ_cd_id, src_cls_cd_id, trg_cls_cd_id, err_desc) 
	VALUES 
		('8fa25b69-c9c2-4c40-84c1-0ea9641a12ec', 'bacd9c6f-3fa9-481e-9636-37457962804d', '9de2a846-ddf2-4ebc-902e-84508c5089ea', 'Patient -[AdoptedChild]->Person'),
		('8fa25b69-c9c2-4c40-84c1-0ea9641a12ec', 'bacd9c6f-3fa9-481e-9636-37457962804d', 'bacd9c6f-3fa9-481e-9636-37457962804d', 'Patient -[AdoptedChild]->Patient'),
		('abfe2637-d338-4090-b3a5-3ec19a47be6a', 'bacd9c6f-3fa9-481e-9636-37457962804d', '9de2a846-ddf2-4ebc-902e-84508c5089ea', 'Patient -[FosterChild]->Person'),
		('abfe2637-d338-4090-b3a5-3ec19a47be6a', 'bacd9c6f-3fa9-481e-9636-37457962804d', 'bacd9c6f-3fa9-481e-9636-37457962804d', 'Patient -[FosterChild]->Patient'),
		('decd6250-7e8b-4b77-895d-31953cf1387a', 'bacd9c6f-3fa9-481e-9636-37457962804d', '9de2a846-ddf2-4ebc-902e-84508c5089ea', 'Patient -[FosterSon]->Person'),
		('decd6250-7e8b-4b77-895d-31953cf1387a', 'bacd9c6f-3fa9-481e-9636-37457962804d', 'bacd9c6f-3fa9-481e-9636-37457962804d', 'Patient -[FosterSon]->Patient'),
		('e81d6773-97e3-4b2d-b6a3-a4624ba5c6a9', 'bacd9c6f-3fa9-481e-9636-37457962804d', '9de2a846-ddf2-4ebc-902e-84508c5089ea', 'Patient -[FosterDaughter]->Person'),
		('e81d6773-97e3-4b2d-b6a3-a4624ba5c6a9', 'bacd9c6f-3fa9-481e-9636-37457962804d', 'bacd9c6f-3fa9-481e-9636-37457962804d', 'Patient -[FosterDaughter]->Patient')
ON CONFLICT DO NOTHING;

 SELECT REG_PATCH('20260720-01');