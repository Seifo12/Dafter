namespace CourtDesk.Resources
{
    public static class Strings
    {
        public const string AppTitle = "سجل المحبوسين والجلسات";

        public const string NavRegister = "السجل";
        public const string NavSchedule = "الجدول";
        public const string NavHistory = "القرارات";
        public const string NavBackup = "النسخ والإعدادات";
        public const string NavHelp = "مساعدة";
        public const string NavExit = "خروج";

        public const string FontSmaller = "حرف أصغر";
        public const string FontLarger = "حرف أكبر";

        public const string SearchPlaceholder = "اكتب اسم المتهم أو رقم القضية للبحث…";
        public const string SearchClear = "مسح";
        public const string SearchNone = "مفيش نتايج للبحث ده.";
        public const string SearchNoneInFilter = "مفيش نتايج بين «{0}». فيه {1} نتيجة في «الكل».";
        public const string SearchShowAll = "اعرض الكل";

        public const string FilterDetained = "المحبوسون";
        public const string FilterAll = "الكل";
        public const string FilterReleased = "المفرج عنهم";
        public const string FilterJudged = "صدر فيهم حكم";
        public const string FilterNoDate = "بدون ميعاد";

        public const string ActionAdd = "إضافة متهم";
        public const string ActionEdit = "تعديل";
        public const string ActionDecide = "تسجيل قرار";
        public const string ActionCard = "بطاقة المتهم";
        public const string ActionVoid = "إلغاء السجل";

        public const string HintSelectRow = "اختار متهم من الجدول الأول.";

        public const string GridIndex = "م";
        public const string GridName = "الاسم";
        public const string GridCase = "الاسم والقضية";
        public const string GridCourt = "المحكمة / الدائرة";
        public const string GridSession = "ميعاد الجلسة";
        public const string GridState = "الحالة";
        public const string GridCharges = "التهمة";
        public const string GridCustody = "مكان الحجز";

        public const string GridWideOn = "عرض كل الأعمدة";
        public const string GridWideOff = "عرض مختصر";

        public const string CaseFormat = "{0} لسنة {1}";

        public const string StatusDetained = "محبوس";
        public const string StatusReleased = "مُفرج عنه";
        public const string StatusJudged = "حكم";

        public const string StateToday = "اليوم";
        public const string StateOverdue = "فاتت";
        public const string StateUpcoming = "قادمة";
        public const string StateNoDate = "بدون ميعاد";

        public const string OverdueDays = "فاتت من {0} يوم";

        public const string SideTitle = "جلسات اليوم";
        public const string SideProgress = "تم تسجيل {0} من {1}";
        public const string SidePending = "في انتظار القرار";
        public const string SideDecided = "تم تسجيل القرار";
        public const string SideOverdue = "جلسات فاتت من غير قرار ({0})";
        public const string SideTomorrow = "جلسات بكرة: {0}";
        public const string SideEmpty = "مفيش جلسات النهارده.";
        public const string SideRecord = "سجّل القرار";
        public const string SideQueue = "سجّل بالترتيب";
        public const string SideUndo = "تراجع";
        public const string SidePrint = "طباعة كشف الترحيل";
        public const string SidePreview = "معاينة";

        public const string PrintSent = "تم إرسال الكشف للطابعة.";
        public const string PrintFail = "مش قادرين نطبع (مفيش طابعة أو الطابعة مش شغالة). فتحنا لك المعاينة.";
        public const string PrintEmpty = "مفيش متهمين في كشف النهارده.";
        public const string PrintChoose = "اختيار طابعة تانية";

        public const string FormAddTitle = "إضافة متهم جديد";
        public const string FormEditTitle = "تعديل بيانات المتهم";

        public const string FieldName = "اسم المتهم (رباعي)";
        public const string FieldNationalId = "الرقم القومي (اختياري)";
        public const string FieldCaseNumber = "رقم القضية";
        public const string FieldCaseYear = "سنة القضية";
        public const string FieldCaseType = "نوع القضية";
        public const string FieldCharges = "التهمة";
        public const string FieldCourt = "المحكمة";
        public const string FieldCircuit = "الدائرة";
        public const string FieldCustody = "مكان الحجز";
        public const string FieldSessionDate = "ميعاد الجلسة";
        public const string FieldDetainedSince = "محبوس من تاريخ";
        public const string FieldNotes = "ملاحظات";
        public const string FieldRequired = "(مطلوب)";
        public const string FieldNoDate = "بدون ميعاد";

        public const string DateToday = "اليوم";
        public const string DateTomorrow = "بكرة";

        public const string AddNew = "+ إضافة جديد";
        public const string AddNewCircuit = "+ إضافة دائرة جديدة";

        public const string FormSave = "حفظ";
        public const string FormCancel = "إلغاء";

        public const string ValName = "اكتب اسم المتهم.";
        public const string ValCaseNumber = "اكتب رقم القضية (أرقام فقط).";
        public const string ValCaseYear = "اكتب سنة القضية (4 أرقام).";
        public const string ValNationalId = "الرقم القومي لازم يكون 14 رقم.";
        public const string ValDateInvalid = "التاريخ مش صحيح.";
        public const string ValCircuitCourt = "الدائرة دي مش تابعة للمحكمة المختارة.";
        public const string ValSummary = "الحفظ مش ممكن لأن:";

        public const string DupWarn = "فيه متهم مسجل بنفس الاسم ونفس رقم القضية. تحب تفتح السجل الموجود ولا تكمّل الإضافة؟";
        public const string DupOpen = "فتح السجل الموجود";
        public const string DupAdd = "إضافة برضه";

        public const string Saved = "اتحفظت البيانات.";

        public const string VoidTitle = "إلغاء السجل";
        public const string VoidSentence = "هتلغي سجل {0} (قضية {1}). السجل مش هيظهر في القوايم، وتقدر تسترجعه بعدين من سلة المحذوفات.";
        public const string VoidReason1 = "إدخال خاطئ";
        public const string VoidReason2 = "مكرر";
        public const string VoidYes = "أيوه، إلغاء السجل";
        public const string VoidNo = "لأ، سيبه";

        public const string TrashRestore = "استرجاع السجل";

        public const string WizTitle = "تسجيل قرار الجلسة";
        public const string WizStep = "الخطوة {0} من {1}";
        public const string WizQ1 = "إيه قرار المحكمة؟";

        public const string DecRenew = "تجديد الحبس";
        public const string DecPostpone = "تأجيل الجلسة";
        public const string DecRelease = "إخلاء سبيل";
        public const string DecBail = "إخلاء سبيل بكفالة";
        public const string DecJudgment = "صدر حكم";
        public const string DecReopen = "إعادة للحبس";

        public const string WizSessionHeld = "جلسة يوم {0}";
        public const string WizQ2 = "ميعاد الجلسة الجاية امتى؟";
        public const string WizOffset = "بعد {0} يوم";
        public const string WizPickDate = "اختار يوم تاني من التقويم";
        public const string WizChosen = "الجلسة الجاية: {0} (بعد {1} يوم)";
        public const string WizReason = "السبب (اختياري)";
        public const string WizChangeCourt = "تغيير المحكمة أو الدائرة";
        public const string WizBailAmount = "مبلغ الكفالة بالجنيه (اختياري)";
        public const string WizNote = "ملاحظات (اختياري)";
        public const string WizJudgmentNote = "منطوق الحكم (اختياري)";

        public const string WarnWeekend = "اليوم ده {0} والمحاكم غالبًا مقفولة.";
        public const string WarnPast = "التاريخ ده في الماضي.";
        public const string WarnFar = "التاريخ بعيد (بعد {0} يوم).";
        public const string WarnConfirm = "أيوه، متأكد من التاريخ ده";

        public const string ErrNextRequired = "اختار ميعاد الجلسة الجاية الأول.";
        public const string ErrNextAfter = "ميعاد الجلسة الجاية لازم يكون بعد ميعاد الجلسة الحالية ({0}).";
        public const string ErrStale = "السجل ده اتغيّر وانت بتسجل. افتحه من جديد وراجع بياناته.";
        public const string ErrFutureSession = "ميعاد جلسة المتهم ده لسه ماجاش ({0}). لو الميعاد غلط، عدّله من «تعديل البيانات».";
        public const string ErrNoSession = "المتهم ده مالوش ميعاد جلسة متسجل. عدّل بياناته وحط الميعاد الأول.";
        public const string ErrGeneric = "حصلت مشكلة بسيطة. بياناتك في أمان. لو اتكررت بلّغ المسؤول برقم الخطأ: {0}";
        public const string ErrReopenRecord = "افتح السجل من جديد";

        public const string WizQ3 = "راجع قبل الحفظ";

        public const string WizSentenceRenew = "المحكمة قررت تجديد حبس {0} (قضية {1})، والجلسة الجاية يوم {2}.";
        public const string WizSentencePostpone = "المحكمة قررت تأجيل قضية {0} ({1}) لجلسة يوم {2}.";
        public const string WizSentenceRelease = "المحكمة قررت إخلاء سبيل {0} (قضية {1}). هيتنقل لقايمة المفرج عنهم.";
        public const string WizSentenceBail = "المحكمة قررت إخلاء سبيل {0} (قضية {1}) بكفالة {2} جنيه. هيتنقل لقايمة المفرج عنهم.";
        public const string WizSentenceBailNoMoney = "المحكمة قررت إخلاء سبيل {0} (قضية {1}) بكفالة. هيتنقل لقايمة المفرج عنهم.";
        public const string WizSentenceJudgment = "صدر حكم في قضية {0} ({1}). هيتنقل لقايمة «صدر فيهم حكم».";
        public const string WizSentenceReopen = "هيرجع {0} (قضية {1}) لقايمة المحبوسين، والجلسة الجاية يوم {2}.";

        public const string WizSave = "حفظ القرار";
        public const string WizBack = "رجوع";
        public const string WizNext = "التالي";
        public const string WizCancel = "إلغاء";
        public const string WizClose = "إغلاق";

        public const string WizSaving = "جارٍ الحفظ…";
        public const string WizDone = "اتحفظ القرار.";
        public const string WizNextDateLine = "الجلسة الجاية: {0}";
        public const string WizMovedTo = "اتنقل لقايمة «{0}».";
        public const string WizNextPerson = "المتهم الجاي: {0}";
        public const string WizQueueProgress = "{0} من {1}";
        public const string WizQueueStop = "وقّف هنا";
        public const string WizUndo = "تراجع عن القرار ده";

        public const string UndoConfirm = "هتتراجع عن قرار «{0}» للمتهم {1} وترجّع بياناته زي ما كانت. متأكد؟";
        public const string UndoYes = "أيوه، تراجع";
        public const string UndoNo = "لأ، سيبه";

        public const string ExitConfirm = "تقفل البرنامج؟";
        public const string ExitYes = "أيوه، اقفل";
        public const string ExitNo = "لأ، ارجع";

        public const string Ok = "تمام";
        public const string SideTabToday = "اليوم ({0})";
        public const string SideTabOverdue = "فاتت ({0})";
        public const string FontLabel = "حجم الحرف";
        public const string GridDataTitle = "بيانات القضية";
        public const string SidePrintShort = "طباعة كشف الترحيل";
        public const string CustodyHeld = "محتجز";
        public const string CustodyFree = "غير محتجز";
        public const string HistWhen = "وقت القرار";
        public const string ExcelEmpty = "الملف فاضي أو فيه صف واحد بس.";
        public const string ExcelMissingColumn = "الملف ناقص العمود:";
        public const string ExcelRowProblem = "سطر";
        public const string ExcelTitle = "الملف الطبي والاستيراد";
        public const string SideMoreDecidedPlus = "+{0}";
        public const string SideShowAll = "عرض الكل";
        public const string SideShowLess = "عرض أقل";
        public const string TomorrowShort = "بكرة: {0}";
        public const string CalPrev = "السابق";
        public const string CalNext = "التالي";
        public const string CalSat = "السبت";
        public const string CalSun = "الأحد";
        public const string CalMon = "الاثنين";
        public const string CalTue = "الثلاثاء";
        public const string CalWed = "الأربعاء";
        public const string CalThu = "الخميس";
        public const string CalFri = "الجمعة";
        public const string SideTitleUpcoming = "الأيام الجاية";
        public const string CardHistoryTitle = "سجل القرارات";
        public const string PrintSend = "طباعة";
        public const string PreviewTitle = "معاينة قبل الطباعة";
        public const string CrashUserMessage = "حصل خطأ غير متوقع. تم حفظ بياناتك. من فضلك أعد تشغيل البرنامج.";
        public const string BkTier = "النوع";
        public const string BkFile = "اسم الملف";
        public const string BkSize = "الحجم";
        public const string BkDate = "تاريخ النسخ";
        public const string FormYes = "نعم";
        public const string FormNo = "لا";
        public const string ToastSaved = "تم الحفظ";
        public const string StatusRecordsUnit = "سجل";
        public const string OperatorUnknown = "غير مسجل";
        public const string SplashWait = "برجاء الانتظار…";

        public const string BkTitle = "النسخ الاحتياطي";
        public const string BkUsb = "حفظ نسخة على الفلاشة";
        public const string BkUsbInsert = "ادخل الفلاشة في الجهاز، وهنكمل لوحدنا أول ما نشوفها.";
        public const string BkUsbRetry = "حاول تاني";
        public const string BkUsbChoose = "اختار الفلاشة اللي عايز تحفظ عليها:";
        public const string BkUsbOk = "اتحفظت النسخة بنجاح على الفلاشة ({0}). تقدر تسحبها بعد ما تضغط «إزالة الجهاز بأمان».";
        public const string BkUsbFail = "ما اتحفظتش النسخة. {0}";
        public const string BkUsbFailRemoved = "الفلاشة اتشالت أثناء الحفظ.";
        public const string BkUsbFailFull = "مفيش مساحة كفاية على الفلاشة.";
        public const string BkUsbFailRo = "الفلاشة مقفولة للكتابة.";
        public const string BkUsbFailUnknown = "حصلت مشكلة غير متوقعة.";
        public const string BkLastUsb = "آخر نسخة على الفلاشة: ‎{0}‎";
        public const string BkUsbOld = "آخر نسخة على الفلاشة كانت من ‎{0}‎ يوم. الأفضل تحفظ نسخة جديدة دلوقتي.";
        public const string BkNeverUsb = "لسه ماحفظتش نسخة على الفلاشة.";
        public const string BkLastExit = "آخر نسخة عند الخروج: ‎{0}‎";
        public const string BkRestore = "استرجاع نسخة قديمة";
        public const string BkRestoreCompare = "النسخة المختارة: {0} متهم، بتاريخ {1}. النسخة الحالية: {2} متهم.";
        public const string BkRestoreSafe = "هنحفظ نسخة من بياناتك الحالية الأول عشان ماتضيعش.";
        public const string BkRestoreGo = "استرجاع";
        public const string BkRestoreBack = "رجوع";
        public const string BkCsv = "تصدير كل المتهمين لملف إكسل";
        public const string BkAutoNote = "البرنامج بيحفظ نسخة لوحده كل ما تقفله وكل شوية أثناء الشغل.";
        public const string BkOpenFolder = "فتح مكان البيانات";

        public const string RecoverNotice = "حصلت مشكلة بسبب انقطاع الكهربا. رجّعنا آخر نسخة سليمة بتاريخ {0}. ممكن تكون آخر عمليات اتسجلت من حوالي {1} دقيقة اتفقدت، راجع آخر القرارات.";
        public const string RecoverUnclean = "البرنامج اتقفل فجأة المرة اللي فاتت (غالبًا الكهربا قطعت). اتفحصت البيانات وهي سليمة.";
        public const string RecoverNone = "مفيش نسخة سليمة نرجّعها. الملف التالف اتحفظ على جنب ومامسّيناش. بلّغ المسؤول.";
        public const string RecoverNewDb = "ابدأ قاعدة جديدة فاضية";
        public const string RecoverClose = "إغلاق";

        public const string BusyOther = "البرنامج مفتوح في مكان تاني.";
        public const string DbNewer = "قاعدة البيانات أحدث من البرنامج.";

        public const string WarnFs = "القرص ده مش NTFS، والأفضل تحفظ نسخة على الفلاشة كل يوم.";
        public const string WarnSpace = "المساحة الفاضية على الجهاز قليلة.";

        public const string ClockWarn = "تاريخ الجهاز غلط على الأغلب. التاريخ الحالي {0} لكن آخر استخدام للبرنامج كان {1}. صحّح التاريخ في ويندوز قبل ما تسجل أي قرار.";
        public const string ClockOpen = "فتح إعدادات التاريخ";
        public const string ClockOk = "التاريخ صحيح — كمّل";
        public const string ClockBanner = "تاريخ الجهاز مشكوك فيه";

        public const string RolloverToast = "اليوم اتغيّر — اتحدّثت قايمة جلسات اليوم.";

        public const string InvariantWarn = "تنبيه: فيه تعارض في بيانات متهم رقم {0}. كمّل شغلك وبلّغ المسؤول.";

        public const string FirstRunTitle = "أهلًا بيك";
        public const string FirstRunStation = "اسم القسم";
        public const string FirstRunDirectorate = "اسم المديرية";
        public const string FirstRunOperator = "اسم القائم بالتسجيل";
        public const string FirstRunGo = "ابدأ";

        public const string SettingsOffsets = "الأيام السريعة";
        public const string SettingsDirectPrint = "الطباعة المباشرة";
        public const string SettingsLookups = "إدارة القوائم";
        public const string SettingsTrash = "سلة المحذوفات";

        public const string HelpTitle = "مساعدة";
        public const string Help1 = "1- دوّر على المتهم: اكتب جزء من الاسم أو رقم القضية في خانة البحث.";
        public const string Help2 = "2- جلسات النهارده: القايمة اللي على اليمين فيها كل اللي عندهم جلسة. اضغط «طباعة كشف الترحيل» وهتطلع الورقة.";
        public const string Help3 = "3- لما قوة الترحيل ترجع: اضغط «تسجيل القرار» قدام كل اسم، واختار قرار المحكمة، وحدد ميعاد الجلسة الجاية، وبعدين «حفظ القرار».";
        public const string Help4 = "4- لو غلطت: بعد الحفظ اضغط «تراجع عن القرار ده».";
        public const string Help5 = "5- النسخ الاحتياطي: البرنامج بيحفظ نسخة لوحده. كل كام يوم ادخل فلاشة واضغط «حفظ نسخة على الفلاشة».";
        public const string Help6 = "6- لو الكهربا قطعت: ماتقلقش، اللي اتحفظ ما بيضيعش. افتح البرنامج تاني وكمّل.";

        public const string RosterMinistry = "وزارة الداخلية";
        public const string RosterDirectorate = "مديرية أمن {0}";
        public const string RosterStation = "قسم شرطة {0}";
        public const string RosterTitle = "كشف ترحيل المحبوسين إلى المحاكم";
        public const string RosterDate = "ليوم {0} الموافق {1}";
        public const string RosterCount = "إجمالي عدد المحبوسين: {0}‏";
        public const string RosterPage = "صفحة {0} من {1}";
        public const string RosterPrinted = "طُبع بتاريخ {0} الساعة {1}";
        public const string RosterSigPrepared = "القائم بالتحضير";
        public const string RosterSigCustody = "ضابط الحجز";
        public const string RosterSigEscort = "رئيس قوة الترحيل (استلام)";
        public const string RosterSigChief = "مأمور / رئيس القسم";
        public const string RosterSigCaption = "الاسم والتوقيع";
        public const string RosterReceipt = "وقت التسليم: ______ تاريخ: ______";

        public const string CsvHeaders = "م, الاسم, الرقم القومي, رقم القضية, سنة القضية, نوع القضية, التهمة, المحكمة, الدائرة, مكان الحجز, ميعاد الجلسة, الحالة, محبوس من, ملاحظات";

        public static readonly string[] RosterCols = new string[]
        {
            "م", "اسم المتهم", "رقم القضية", "التهمة", "المحكمة / الدائرة", "مكان الحجز", "ملاحظات"
        };

        public static readonly string[] SeedCourts = new string[]
        {
            "محكمة الجنح", "محكمة الجنايات", "النيابة العامة"
        };

        public static readonly string[] SeedCustody = new string[]
        {
            "حجز القسم"
        };

        public static readonly string[] SeedReasons = new string[]
        {
            "للاطلاع", "للمرافعة", "لسماع الشهود", "لحضور المتهم", "لتقديم مستندات", "للنطق بالحكم"
        };

        public static readonly string[] CaseTypes = new string[]
        {
            "جنحة", "جناية", "مخالفة", "إدارية", "أخرى"
        };

        public static readonly string[] Weekdays = new string[]
        {
            "الأحد", "الاثنين", "الثلاثاء", "الأربعاء", "الخميس", "الجمعة", "السبت"
        };

        public static readonly string[] Months = new string[]
        {
            "يناير", "فبراير", "مارس", "أبريل", "مايو", "يونيو",
            "يوليو", "أغسطس", "سبتمبر", "أكتوبر", "نوفمبر", "ديسمبر"
        };
    }
}
