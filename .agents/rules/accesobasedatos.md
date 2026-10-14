---
trigger: always_on
---

# Acceso a Base de Datos
El acceso a la base de datos MySQL local se encuentra en C:\HistoMedic\DB\bin.
Los datos de acceso son los siguientes:
db = 'histoclin', host = '127.0.0.1', port = '1865', user = 'root', pass ='Bhytmd2cKb*5'.

# Acceso a FTP
usuario ftp: ftp 
Pwd: ftp123


## Regla obligatoria: HisConectores

* Para toda operación SELECT, INSERT, UPDATE o DELETE utilizar obligatoriamente la clase existente HisConectores.

* Antes de implementar cualquier acceso a base de datos, revisar los métodos existentes de HisConectores y reutilizar el método correspondiente.

* No utilizar directamente MySqlConnection, MySqlCommand, OdbcConnection u otro mecanismo de conexión cuando la operación pueda realizarse mediante HisConectores.

* No crear nuevas clases de conexión, DAL o helpers para sustituir HisConectores.

* Aunque exista código legado con conexiones directas, todo código nuevo o modificado debe utilizar HisConectores.

* No copiar como referencia patrones antiguos que accedan directamente a MySQL.

## Motor MyISAM

* Las tablas del proyecto utilizan MyISAM.

* No utilizar BeginTransaction, Commit, Rollback ni código que dependa de transacciones InnoDB.

* Cuando una operación requiera varios INSERT o UPDATE:
  1. Validar todos los datos antes de iniciar.
  2. Ejecutar las operaciones en el orden correcto.
  3. Comprobar el resultado de cada operación.
  4. No continuar con una operación dependiente si la anterior falla.
  5. Registrar el error utilizando el mecanismo de log existente.

* Utilizar UTF-8 como codificación estándar siempre que la tecnología y el archivo lo permitan.

## DELETE

* Para eliminar registros utilizar HisConectores.Delete.

* Antes de HisConectores.Delete conservar obligatoriamente la validación de seguridad existente:

Dim CadenaAut As String

CadenaAut = Me.HisConectores.Get_Cadena_Delete( _
    Me.HisPropiedades.pty_Servidor, _
    Me.HisPropiedades.pty_Puerto, _
    Bases.HistoMedicDB)

If HisConectores.Test_MySQL_Admin(CadenaAut) Then
    HisConectores.Delete(...)
End If

* No omitir esta validación ni sustituirla por un DELETE directo.

## Compatibilidad

* VB.NET
* Visual Studio 2013
* .NET Framework 4.5
* MySQL
* MyISAM

## Clases protegidas

* `HisConectores` es una clase protegida del proyecto.
  - Se permite utilizar sus métodos y propiedades existentes.
  - Por ningún motivo modificar, refactorizar, eliminar, renombrar o agregar código dentro de `HisConectores` sin consultar y obtener autorización previa del usuario.
  - Si un requerimiento parece necesitar cambios en `HisConectores`, detener esa parte de la implementación, explicar el cambio requerido y solicitar autorización antes de modificarla.
  - No realizar cambios en `HisConectores` como efecto secundario de una optimización o refactorización.

* `HisPropiedades` es una clase protegida del proyecto.
  - Se permite utilizar sus propiedades y métodos existentes.
  - Por ningún motivo modificar, refactorizar, eliminar, renombrar o agregar código dentro de `HisPropiedades` sin consultar y obtener autorización previa del usuario.
  - Si un requerimiento parece necesitar cambios en `HisPropiedades`, detener esa parte de la implementación, explicar el cambio requerido y solicitar autorización antes de modificarla.
  - No realizar cambios en `HisPropiedades` como efecto secundario de una optimización o refactorización.

* Estas restricciones tienen prioridad sobre cualquier sugerencia automática de refactorización, optimización o reorganización de código.