# Allergy bot helper

Este proyecto esta pensado en dar una herramienta "simple" para poder consultar las alergias de una persona. la
idea es simplemente utilziar un bot de Whatsapp/Telegram donde pueda dar las alergias de la persona y con eso
se pueda preguntas sobre cuales son o enviar una imagen sobre los componentes de un producto para saber si puede afectar de alguna manera.

## Deciciones

- **MongoDB**: Para poder utilizar archivos que pertenezcan a una persona, asi como poder almacenar imagenes y analisarlas.
- **C#**: Esta decicion es simplemente por gusto, podria haebr utilizado Python y habria sido mas sencillo, pero tube la necesidad de practicar trabajar con clases y un lenguaje estructurado como C#.

## Comandos y funcionamiento del bot

- /login y /register -> Inicio principal, dependiendo de si se utiliza un token (en login) o correo y contraseña, se determinara si es el ownser o un añadido para poder hacer consultas. Solo el owner tendra privilegios para editar las alergias.

- /compartir -> Genera una clave o token para que las personas puedan ingresar solo en modo read-only con esa clave.
- /revocar -> Elimina la clave o token.

La idea es que estos 2 funcionen en conjunto y solo pueda acceder el owner.

Para manejar las alergias:

- /Add -> Añadir alergia mediante texto, lista o fotografia. Solo owner.
- /Remove -> Quitar alergia mediante texto o lista. Solo owner.
- /Listar -> Listar las alergias. Usable por cualquiera.

El resto del bot (sin comandos), funcionara sin necesidad de comandos, asumiendo solo lectura. La primera vez que se inicie pedira el login o register de forma obligatoria.
