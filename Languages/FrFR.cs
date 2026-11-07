using System.Collections.Generic;

namespace WinLauncher.Languages
{
    /// <summary>Français (France) — terminologie reprise de Windows 11 fr-FR</summary>
    public class FrFR : ILanguage
    {
        public string Code { get { return "fr-FR"; } }
        public string DisplayName { get { return "Français (France)"; } }
        public bool RightToLeft { get { return false; } }

        public Dictionary<string, string> Strings
        {
            get
            {
                return new Dictionary<string, string>
                {
                    { "Menu", "Menu et paramètres" },
                    { "LayoutStyle", "Style du menu Démarrer" },
                    { "Theme", "Thème clair ou sombre" },
                    { "Dark", "Sombre" },
                    { "Light", "Clair" },
                    { "MetroBg", "Arrière-plan Windows 8" },
                    { "Skins", "Thèmes importés (clic droit pour supprimer)" },
                    { "ImportSkin", "+ Importer un thème JSON" },
                    { "ConfigSection", "Configuration" },
                    { "ImportConfig", "Importer une ancienne configuration" },
                    { "ExportConfig", "Exporter la configuration actuelle" },
                    { "ResetConfig", "Tout réinitialiser" },
                    { "Language", "Langue" },

                    { "Search", "Rechercher" },
                    { "SearchTip", "Rechercher des applications, des paramètres et des documents" },
                    { "SearchPrograms", "Rechercher des programmes et des fichiers" },

                    { "Pinned", "Épinglé" },
                    { "PinnedToStart", "Épingler à l’écran d’accueil" },
                    { "All", "Tout" },
                    { "AllApps", "Toutes les applications" },
                    { "ShowAll", "Tout afficher" },
                    { "Collapse", "Réduire" },
                    { "PinnedHeader", "Épinglé" },

                    { "Documents", "Documents" },
                    { "Pictures", "Images" },
                    { "Music", "Musique" },
                    { "Computer", "Ordinateur" },
                    { "Downloads", "Téléchargements" },
                    { "ControlPanel", "Panneau de configuration" },
                    { "Devices", "Périphériques et imprimantes" },
                    { "DefaultPrograms", "Programmes par défaut" },
                    { "Help", "Aide et support" },
                    { "Run", "Exécuter..." },

                    { "MyDocuments", "Mes documents" },
                    { "MyPictures", "Mes images" },
                    { "MyMusic", "Ma musique" },
                    { "MyComputer", "Poste de travail" },
                    { "Printers", "Imprimantes et télécopieurs" },
                    { "SearchText", "Rechercher" },

                    { "SignOut", "Se déconnecter" },
                    { "ShutDown", "Arrêter" },
                    { "PowerOff", "Arrêter" },
                    { "Restart", "Redémarrer" },
                    { "Sleep", "Veille" },
                    { "Lock", "Verrouiller" },
                    { "Power", "Alimentation" },
                    { "Settings", "Paramètres" },
                    { "UserAccount", "Utilisateur actuel" },

                    { "PinTaskbar", "Épingler à la barre des tâches" },
                    { "OpenLocation", "Ouvrir l’emplacement du fichier" },
                    { "RunAsAdmin", "Exécuter en tant qu’administrateur" },
                    { "Unpin", "Désépingler de l’écran d’accueil" },
                    { "CustomColor", "Couleur personnalisée" },
                    { "Small", "Petite" },
                    { "Medium", "Moyenne" },
                    { "Wide", "Large" },
                    { "Large", "Grande" },
                    { "RenameGroup", "Renommer le groupe" },
                    { "DeleteGroup", "Supprimer le groupe (avec les vignettes)" },
                    { "NewGroupHint", "+ Faites glisser ici pour créer un groupe" },
                    { "AllPrograms", "Tous les programmes" }
                };
            }
        }
    }
}
