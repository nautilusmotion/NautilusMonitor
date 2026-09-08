// NauTilus Monitor - Localizacion (i18n)
// Orden de idiomas en cada array: En, Es, Ru, Fr, De, Pt

using System.Collections.Generic;

namespace NautilusMotion.Monitor
{
    public enum Lang { En = 0, Es = 1, Ru = 2, Fr = 3, De = 4, Pt = 5 }

    public static class Loc
    {
        public static Lang Current = Lang.En;

        public static readonly string[] Names = { "English", "Español", "Русский", "Français", "Deutsch", "Português" };
        public static readonly string[] SubNames = { "English", "Spanish", "Russian", "French", "German", "Portuguese" };

        public static string T(string key)
        {
            string[] a;
            if (S.TryGetValue(key, out a))
            {
                int i = (int)Current;
                if (i < a.Length && !string.IsNullOrEmpty(a[i])) return a[i];
                return a[0];
            }
            return key;
        }

        private static readonly Dictionary<string, string[]> S = new Dictionary<string, string[]>
        {
            {"hero.title", new[]{
                "Monitor your PC in real time",
                "Monitoriza tu PC en tiempo real",
                "Мониторинг ПК в реальном времени",
                "Surveillez votre PC en temps réel",
                "Überwachen Sie Ihren PC in Echtzeit",
                "Monitore o seu PC em tempo real"}},
            {"hero.sub", new[]{
                "Usage, clock speeds, temperatures and fans — live, private and lightweight.",
                "Uso, frecuencias, temperaturas y ventiladores — en vivo, privado y ligero.",
                "Загрузка, частоты, температуры и вентиляторы — вживую, приватно и легко.",
                "Utilisation, fréquences, températures et ventilateurs — en direct, privé et léger.",
                "Auslastung, Taktraten, Temperaturen und Lüfter — live, privat und schlank.",
                "Uso, frequências, temperaturas e ventoinhas — ao vivo, privado e leve."}},

            {"start.msg", new[]{"Starting sensors...","Iniciando sensores...","Запуск датчиков...","Démarrage des capteurs...","Sensoren werden gestartet...","Iniciando sensores..."}},

            // Gauges
            {"g.cpu", new[]{"CPU","CPU","ЦП","CPU","CPU","CPU"}},
            {"g.gpu", new[]{"GPU","GPU","ГП","GPU","GPU","GPU"}},
            {"g.ram", new[]{"RAM","RAM","ОЗУ","RAM","RAM","RAM"}},
            {"g.disk", new[]{"DISK","DISCO","ДИСК","DISQUE","DATENTR.","DISCO"}},

            // Cabeceras de fichas
            {"card.cpu", new[]{"PROCESSOR","PROCESADOR","ПРОЦЕССОР","PROCESSEUR","PROZESSOR","PROCESSADOR"}},
            {"card.percore", new[]{"PER-CORE USAGE","USO POR NÚCLEO","ЗАГРУЗКА ПО ЯДРАМ","UTILISATION PAR CŒUR","AUSLASTUNG JE KERN","USO POR NÚCLEO"}},
            {"card.temps", new[]{"TEMPERATURES","TEMPERATURAS","ТЕМПЕРАТУРЫ","TEMPÉRATURES","TEMPERATUREN","TEMPERATURAS"}},
            {"card.fans", new[]{"FANS","VENTILADORES","ВЕНТИЛЯТОРЫ","VENTILATEURS","LÜFTER","VENTOINHAS"}},
            {"card.disk", new[]{"DISK ACTIVITY","ACTIVIDAD DEL DISCO","АКТИВНОСТЬ ДИСКА","ACTIVITÉ DISQUE","DATENTRÄGER","ATIVIDADE DO DISCO"}},
            {"card.net", new[]{"NETWORK","RED","СЕТЬ","RÉSEAU","NETZWERK","REDE"}},
            {"card.system", new[]{"SYSTEM","SISTEMA","СИСТЕМА","SYSTÈME","SYSTEM","SISTEMA"}},

            // Etiquetas
            {"lbl.clock", new[]{"Clock","Reloj","Частота","Fréquence","Takt","Relógio"}},
            {"lbl.base", new[]{"base","base","база","base","Basis","base"}},
            {"lbl.cores", new[]{"cores","núcleos","ядер","cœurs","Kerne","núcleos"}},
            {"lbl.threads", new[]{"threads","hilos","потоков","threads","Threads","threads"}},
            {"lbl.cpus", new[]{"CPUs","CPU","ЦП","CPU","CPUs","CPUs"}},
            {"core.group", new[]{
                "Showing {n} of {m} threads (current processor group)",
                "Mostrando {n} de {m} hilos (grupo de procesador actual)",
                "Показаны {n} из {m} потоков (текущая группа процессоров)",
                "Affichage de {n} sur {m} threads (groupe de processeurs actuel)",
                "{n} von {m} Threads angezeigt (aktuelle Prozessorgruppe)",
                "Mostrando {n} de {m} threads (grupo de processadores atual)"}},
            {"lbl.used", new[]{"Used","Usada","Занято","Utilisée","Belegt","Usada"}},
            {"lbl.processes", new[]{"Processes","Procesos","Процессы","Processus","Prozesse","Processos"}},
            {"lbl.uptime", new[]{"Uptime","Encendido","Время работы","Temps de marche","Betriebszeit","Tempo ligado"}},
            {"lbl.os", new[]{"System","Sistema","Система","Système","System","Sistema"}},
            {"lbl.board", new[]{"Motherboard","Placa base","Мат. плата","Carte mère","Hauptplatine","Placa-mãe"}},

            {"net.down", new[]{"Download","Bajada","Приём","Réception","Download","Download"}},
            {"net.up", new[]{"Upload","Subida","Отдача","Envoi","Upload","Upload"}},
            {"disk.read", new[]{"Read","Lectura","Чтение","Lecture","Lesen","Leitura"}},
            {"disk.write", new[]{"Write","Escritura","Запись","Écriture","Schreiben","Escrita"}},
            {"disk.active", new[]{"Active","Actividad","Активность","Activité","Aktiv","Atividade"}},

            {"na", new[]{"not available","no disponible","недоступно","non disponible","nicht verfügbar","indisponível"}},

            {"temps.none", new[]{
                "No temperature sensors available. Enable advanced sensors to read them.",
                "No hay sensores de temperatura disponibles. Activa los sensores avanzados para leerlos.",
                "Датчики температуры недоступны. Включите расширенные датчики для их чтения.",
                "Aucun capteur de température disponible. Activez les capteurs avancés pour les lire.",
                "Keine Temperatursensoren verfügbar. Aktivieren Sie erweiterte Sensoren, um sie zu lesen.",
                "Sem sensores de temperatura disponíveis. Ative os sensores avançados para lê-los."}},
            {"fans.none", new[]{
                "No fan sensors available.",
                "No hay sensores de ventiladores disponibles.",
                "Датчики вентиляторов недоступны.",
                "Aucun capteur de ventilateur disponible.",
                "Keine Lüftersensoren verfügbar.",
                "Sem sensores de ventoinhas disponíveis."}},

            // Sensores avanzados
            {"adv.enable", new[]{"Enable advanced sensors","Activar sensores avanzados","Включить расширенные датчики","Activer les capteurs avancés","Erweiterte Sensoren aktivieren","Ativar sensores avançados"}},
            {"adv.on", new[]{"Advanced sensors on","Sensores avanzados activos","Расширенные датчики включены","Capteurs avancés actifs","Erweiterte Sensoren aktiv","Sensores avançados ativos"}},
            {"adv.enabling", new[]{"Enabling...","Activando...","Включение...","Activation...","Aktivierung...","Ativando..."}},
            {"adv.hint", new[]{
                "Temperatures and fan speeds need advanced sensors. This loads a signed driver and asks for administrator once.",
                "Las temperaturas y las RPM de ventiladores necesitan los sensores avanzados. Carga un driver firmado y pide administrador una vez.",
                "Температуры и обороты вентиляторов требуют расширенных датчиков. Загружается подписанный драйвер и один раз запрашиваются права администратора.",
                "Les températures et les vitesses de ventilateurs nécessitent les capteurs avancés. Un pilote signé est chargé et l'administrateur est demandé une fois.",
                "Temperaturen und Lüfterdrehzahlen benötigen erweiterte Sensoren. Ein signierter Treiber wird geladen und einmalig nach Administrator gefragt.",
                "As temperaturas e as RPM das ventoinhas precisam dos sensores avançados. Carrega um driver assinado e pede administrador uma vez."}},
            {"adv.dllmissing", new[]{
                "Advanced sensors need LibreHardwareMonitorLib.dll (MIT) next to the app. Download it from the official releases and place it in this folder.",
                "Los sensores avanzados necesitan LibreHardwareMonitorLib.dll (MIT) junto a la app. Descárgala de las versiones oficiales y colócala en esta carpeta.",
                "Расширенным датчикам нужен LibreHardwareMonitorLib.dll (MIT) рядом с приложением. Скачайте его из официальных релизов и поместите в эту папку.",
                "Les capteurs avancés nécessitent LibreHardwareMonitorLib.dll (MIT) à côté de l'application. Téléchargez-le depuis les versions officielles et placez-le dans ce dossier.",
                "Erweiterte Sensoren benötigen LibreHardwareMonitorLib.dll (MIT) neben der App. Laden Sie sie aus den offiziellen Releases und legen Sie sie in diesen Ordner.",
                "Os sensores avançados precisam do LibreHardwareMonitorLib.dll (MIT) ao lado do app. Baixe-o das versões oficiais e coloque-o nesta pasta."}},
            {"adv.error", new[]{
                "Advanced sensors could not be started.",
                "No se pudieron iniciar los sensores avanzados.",
                "Не удалось запустить расширенные датчики.",
                "Impossible de démarrer les capteurs avancés.",
                "Erweiterte Sensoren konnten nicht gestartet werden.",
                "Não foi possível iniciar os sensores avançados."}},

            // Grabacion de temperaturas
            {"rec.start", new[]{"Record temperatures","Grabar temperaturas","Записать температуры","Enregistrer les températures","Temperaturen aufzeichnen","Gravar temperaturas"}},
            {"rec.on", new[]{"Recording  ●","Grabando  ●","Запись  ●","Enregistrement  ●","Aufzeichnung  ●","Gravando  ●"}},
            {"rec.savingto", new[]{"Saving to:","Guardando en:","Сохранение в:","Enregistrement dans :","Speichern in:","Salvando em:"}},
            {"rec.savedto", new[]{"Saved to:","Guardado en:","Сохранено в:","Enregistré dans :","Gespeichert in:","Salvo em:"}},
            {"rec.openfolder", new[]{"Open folder","Abrir carpeta","Открыть папку","Ouvrir le dossier","Ordner öffnen","Abrir pasta"}},
            {"rec.error", new[]{"Could not start recording.","No se pudo iniciar la grabación.","Не удалось начать запись.","Impossible de démarrer l'enregistrement.","Aufzeichnung konnte nicht gestartet werden.","Não foi possível iniciar a gravação."}},
            {"rec.coltime", new[]{"Date and time","Fecha y hora","Дата и время","Date et heure","Datum und Uhrzeit","Data e hora"}},

            // Bandeja del sistema / segundo plano
            {"tray.show", new[]{"Show","Mostrar","Показать","Afficher","Anzeigen","Mostrar"}},
            {"tray.exit", new[]{"Exit","Salir","Выход","Quitter","Beenden","Sair"}},
            {"tray.background", new[]{
                "NauTilus Monitor keeps running in the background.",
                "NauTilus Monitor sigue ejecutándose en segundo plano.",
                "NauTilus Monitor продолжает работать в фоновом режиме.",
                "NauTilus Monitor continue de fonctionner en arrière-plan.",
                "NauTilus Monitor läuft im Hintergrund weiter.",
                "O NauTilus Monitor continua em execução em segundo plano."}},

            {"footer.dev", new[]{"Developed by ","Desarrollado por ","Разработано ","Développé par ","Entwickelt von ","Desenvolvido por "}},
            {"footer.oss", new[]{
                "  -  Open source  -  MIT License",
                "  -  Código abierto  -  Licencia MIT",
                "  -  Открытый код  -  Лицензия MIT",
                "  -  Open source  -  Licence MIT",
                "  -  Open Source  -  MIT-Lizenz",
                "  -  Código aberto  -  Licença MIT"}},
        };
    }
}
