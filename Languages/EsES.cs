using System.Collections.Generic;

namespace WinLauncher.Languages
{
    /// <summary>Español (España) — terminología como en Windows 11 es-ES</summary>
    public class EsES : ILanguage
    {
        public string Code { get { return "es-ES"; } }
        public string DisplayName { get { return "Español (España)"; } }
        public bool RightToLeft { get { return false; } }

        public Dictionary<string, string> Strings
        {
            get
            {
                return new Dictionary<string, string>
                {
                    { "Menu", "Menú y configuración" },
                    { "LayoutStyle", "Estilo del menú Inicio" },
                    { "Theme", "Tema claro u oscuro" },
                    { "Dark", "Oscuro" },
                    { "Light", "Claro" },
                    { "MetroBg", "Fondo de Windows 8" },
                    { "Skins", "Temas importados (clic derecho para eliminar)" },
                    { "ImportSkin", "+ Importar tema JSON" },
                    { "ConfigSection", "Configuración" },
                    { "ImportConfig", "Importar configuración antigua" },
                    { "ExportConfig", "Exportar configuración actual" },
                    { "ResetConfig", "Restablecer todo" },
                    { "Language", "Idioma" },

                    { "Search", "Buscar" },
                    { "SearchTip", "Buscar aplicaciones, configuración y documentos" },
                    { "SearchPrograms", "Buscar programas y archivos" },

                    { "Pinned", "Anclado" },
                    { "PinnedToStart", "Anclar a Inicio" },
                    { "All", "Todo" },
                    { "AllApps", "Todas las aplicaciones" },
                    { "ShowAll", "Mostrar todo" },
                    { "Collapse", "Contraer" },
                    { "PinnedHeader", "Anclado" },

                    { "Documents", "Documentos" },
                    { "Pictures", "Imágenes" },
                    { "Music", "Música" },
                    { "Computer", "Equipo" },
                    { "Downloads", "Descargas" },
                    { "ControlPanel", "Panel de control" },
                    { "Devices", "Dispositivos e impresoras" },
                    { "DefaultPrograms", "Programas predeterminados" },
                    { "Help", "Ayuda y soporte técnico" },
                    { "Run", "Ejecutar..." },

                    { "MyDocuments", "Mis documentos" },
                    { "MyPictures", "Mis imágenes" },
                    { "MyMusic", "Mi música" },
                    { "MyComputer", "Mi PC" },
                    { "Printers", "Impresoras y faxes" },
                    { "SearchText", "Buscar" },

                    { "SignOut", "Cerrar sesión" },
                    { "ShutDown", "Apagar equipo" },
                    { "PowerOff", "Apagar" },
                    { "Restart", "Reiniciar" },
                    { "Sleep", "Suspender" },
                    { "Lock", "Bloquear" },
                    { "Power", "Energía" },
                    { "Settings", "Configuración" },
                    { "UserAccount", "Usuario actual" },

                    { "PinTaskbar", "Anclar a la barra de tareas" },
                    { "OpenLocation", "Abrir ubicación del archivo" },
                    { "RunAsAdmin", "Ejecutar como administrador" },
                    { "Unpin", "Desanclar de Inicio" },
                    { "CustomColor", "Color personalizado" },
                    { "Small", "Pequeño" },
                    { "Medium", "Mediano" },
                    { "Wide", "Ancho" },
                    { "Large", "Grande" },
                    { "RenameGroup", "Cambiar nombre del grupo" },
                    { "DeleteGroup", "Eliminar grupo (con mosaicos)" },
                    { "NewGroupHint", "+ Arrastra aquí para crear un grupo" },
                    { "AllPrograms", "Todos los programas" }
                };
            }
        }
    }
}
