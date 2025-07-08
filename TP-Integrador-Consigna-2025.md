# Trabajo Práctico de Desarrollo Web

*(version preliminar 0.1)*

[Objetivo General](#objetivo-general)

[Componentes del Proyecto](#componentes-del-proyecto)

[Requerimientos técnicos a aplicar en el presente trabajo](#requerimientos-técnicos-a-aplicar-en-el-presente-trabajo)

[Criterios de Evaluación](#criterios-de-evaluación)

[Bonus](#bonus)

[Entregables](#entregables)

[Rúbrica (Próximamente)](#rúbrica)

## Objetivo General {#objetivo-general}

Desarrollar una solución completa compuesta por tres aplicaciones complementarias, utilizando tecnologías modernas de desarrollo web vistas en clases. Cada grupo definirá su propio escenario temático o dominio de aplicación (por ejemplo: sistema de reservas, gestor de tareas, e-commerce básico, etc.), pero deberán cumplir obligatoriamente con los requisitos funcionales y técnicos detallados a continuación.

## Componentes del Proyecto {#componentes-del-proyecto}

1) **Aplicación Web en ASP.NET MVC**  
   * Proyecto basado en el patrón MVC (Model-View-Controller) con Razor Pages.

2) **API RESTful en ASP.NET Web API**  
   * Proveer endpoints para ser consumidos por la SPA.  
   * Deberá manejar autenticación mediante tokens (por ejemplo, JWT) para el acceso a endpoints protegidos.

3) **Aplicación Web SPA (Single Page Application)**  
   * Desarrollada utilizando un framework JavaScript moderno (FrontEnd). Debe ejecutarse completamente en el cliente (solo debe ir al servidor como API)

## Requerimientos técnicos a aplicar en el presente trabajo {#requerimientos-técnicos-a-aplicar-en-el-presente-trabajo}

1. Proyecto basado en el patrón MVC (Model-View-Controller) con Razor Pages.  
2. Deberá permitir el registro de nuevos usuarios.  
3. Deberá permitir el inicio de sesión mediante usuario y contraseña.  
4. Las contraseñas deberán almacenarse utilizando hash y salt, siguiendo buenas prácticas de seguridad.  
5. Deberá incluir una funcionalidad para la recuperación de contraseña mediante un correo electrónico con un enlace único y seguro por usuario.  
6. Listado paginado de elementos (entidad a definir por el grupo).  
7. Vista de detalle de un elemento específico.  
8. Se debe poder crear un nuevo elemento (por lo menos con campos y uno de ellos un selector/combo/dropdown)  
9. Se debe poder  editar un elemento existente.  
10. En algún punto de la aplicación, se deberá visualizar un código QR generado desde el backend.  
11. Al escanear dicho código (desde un dispositivo móvil o desde la misma web), el usuario podrá acceder a una funcionalidad exclusiva habilitada solo a través del QR.  
    1. El código QR se debe generar en .NET (Web o API), y de tener dentro un enlace  
    2. El enlace debe ir a una web o API “exclusivo”, o sea el enlace debe tener algo que caduque por tiempo (por ejemplo cada 10min, cada hora).   
       Ejemplo que realizamos en clase con “hash”

## Criterios de Evaluación {#criterios-de-evaluación}

* Cumplimiento de los requisitos funcionales y técnicos.  
* Correcta separación de responsabilidades entre capas.  
* Uso adecuado de tecnologías web (MVC, API REST, SPA).  
* Seguridad en el manejo de credenciales y autenticación.  
* Creatividad en la elección y desarrollo del escenario.  
* Documentación básica del sistema (README con instrucciones de uso, herramientas utilizadas y descripción del escenario).

## Bonus {#bonus}

* Implementación de buenas prácticas como validaciones, manejo de errores, diseño responsive o integración continua.

## Entregables {#entregables}

1. Repositorio en GitHub con los tres proyectos claramente diferenciados (carpetas)  
2. Scripts SQL o migraciones para crear la base de datos (en una carpeta posiblemente /db)  
3. Instrucciones para ejecutar cada componente.  
   1. Puede ir en el readme  
4. Capturas o video corto demostrativo del sistema funcionando (carpeta /videos)  
   1. Puede ir en el readme del repo y/o informe  
5. Informe Final: Documento con descripción del escenario elegido y cómo se cumple cada requisito.

## Rúbrica  {#rúbrica}

|  | Requerimientos Tecnicos | [ASP.NET](http://ASP.NET) MVC | Usuario. Password Hasheadas y Recuperar Password | Generar QR dinámico y la url tambien dinámica | Coloquio/Presentación |
| :---- | ----- | ----- | ----- | ----- | ----- |
| Puntos | 30 | 20 | 20 | 20 | 10 |

La asignación de puntaje se basa en el puntaje máximo que se encuentra definido en la rúbrica, y la asignación de dicho puntaje en base a la siguiente tabla para evaluar la entrega.

| Entrega Excelente (100%) | Se aborda el tema. Se presenta la idea y se profundiza la misma o agregando valor. La entrega se realiza en tiempo y forma. El trabajo está estructurado y completado al 10\. El trabajo se presentó con todos los lineamientos propuestos en tiempo y forma. |
| :---- | :---- |
| **Terminado Satisfactorio (80%)** | Se aborda el tema, pero se encuentra en un 75% el punto abordado. La entrega se realiza pero existen puntos faltantes para completar la idea de la funcionalidad requerida. Se entrega en tiempo y forma. No se extendió en lo que se propuso como idea. El trabajo o item falto algunos puntos a tener en cuenta para completarlo. |
| **Básico (60%)** | Se aborda el tema pero con un nivel escaso de comprensión y de realización. Se encuentra realizado al 50% del ítem solicitado. No se extendió en mejorar o perfeccionar. Se encuentra deficiente la organización del trabajo. No se detalla profundidad en parte del ítem o se argumenta. No se presentan todos los lineamientos propuestos para el ítem. |
| **No realizado/Escaso (0%)** | Solo se menciona el tema o no se aborda. No presenta información relacionada al ítem solicitado. O se realizó pero con error en el abordaje para su funcionamiento o publicación. No se estructuró el trabajo. |

