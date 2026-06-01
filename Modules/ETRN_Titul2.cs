//16.06.2023 Зингаров титулы ЭТРН


//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Web;
using Newtonsoft.Json;

namespace tis.Modules
{
  public class ETRN_Titul2
   {
 //------------------------------------------------------------------------------------
   // Главная структура JSON
 //-------------------------------------------------------------------------------------

public const string titul2Add=@"
{     ""СодИнфПрвПрием"": {
           ""ЗамПрвПрием"": {
               ""ЗамДатаПриб"": ""21.07.2023"",
               ""НалКоорТочВрПрб"": 1,
               ""ЗамUTCПриб"": ""+03:00"",
               ""ЗамДатаУбыт"": ""21.07.2023"",
               ""НалКоорТочВрУб"": 1,
               ""ЗамUTCУбыт"": ""+03:00"",
               ""ЗамДатаПосПрием"": ""21.07.2023"",
               ""НалКоорТочВрПрм"": 1,
               ""ЗамUTCПосПрием"": ""+03:00"",
               ""ЗамВремяПриб"": ""10:00:00"",
               ""ЗамВремяУбыт"": ""10:00:00"",
               ""ЗамВремяПосПрием"": ""10:30:00"",
               ""ЗамСостГруз"": ""Груз грязный"",
               ""ЗамМасс"": ""3"",
               ""ЗамПогрРаб"": ""удовлетворительное"",
               ""ЗамКолМест"": ""6""
           }
       },
       ""TRNACLPPRIN"": {
           ""Подписант"": {
               ""СтатПодп"": ""1"",
               ""ФИО"": {
                   ""Фамилия"": ""Павлов"",
                   ""Имя"": ""Константин"",
                   ""Отчество"": ""Сергеевич""
               }
           }
       }
}
";

public const string TITUL2min=@"
{
 ""content"":
 {
	""$$meta$$"": {
		""ИдентификаторПроцесса"": ""0f360005-771c-11ed-a818-4a1be7895515""
	},
	""ЭТРН"": {
		""ВерсПрог"": ""Ecom DocRobot v1"",
		""TRNACLGROT"": {
			""ПоФактХЖ"": ""Транспортная накладная, информация грузоотправителя"",
			""КЭП"": {
				""Организация"": ""ООО Пинокио"",
				""ДействителенДо"": ""21.03.2023"",
				""Отпечаток"": ""750E11C832E17137EA26778F45B14AAD6283BCBA"",
				""ЭП"": ""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"",
				""ДействителенС"": ""21.12.2021"",
				""Сертификат"": ""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"",
				""Должность"": ""Специалист"",
				""Валидность"": ""1"",
				""ФИО"": ""Алан Алборишвили Лонгиевич"",
				""Издатель"": ""УЦ EasyCert"",
				""ДатаВремя"": ""08.12.2022 20:16:33"",
				""СерийныйНомер"": ""011575AC0005AE95BF42F91D84F0D6AD21""
			},
			""ГИС"": {
				""ИмяФайла"": ""ON_TRNACLGROT_2LD06bee784-7719-41f5-9c29-341d7d19a266_2LD9cd9f592-d82c-47e2-bec7-2ab57d397eb1_2LD18a49f63-9ed4-4977-a55e-b6827fbf0eaa_0_20221209_cc64e65b-778a-11ed-a54f-5569bf7d310e.xml"",
				""ИдЗапроса"": ""91d724c2-d4fb-4354-bcae-41b2bc334bab"",
				""КолОтправок"": 1,
				""ИмяФайлаПодписи"": ""ON_TRNACLGROT_2LD06bee784-7719-41f5-9c29-341d7d19a266_2LD9cd9f592-d82c-47e2-bec7-2ab57d397eb1_2LD18a49f63-9ed4-4977-a55e-b6827fbf0eaa_0_20221209_cc64e65b-778a-11ed-a54f-5569bf7d310e.bin"",
				""DocflowId"": ""1012f227-771c-11ed-b659-d111b3752610"",
				""СтатусОбрГИС"": 8,
				""УИД"": ""017cd75d-e8fb-4bc3-b52f-fc53588ad918"",
				""ВремяОтпр"": ""20:16:34"",
				""ДатаПолуч"": ""08.12.2022"",
				""КодСтатуса"": 5000,
				""ОписСтатуса"": ""Принят новый перевозочный документ"",
				""ДатаОтпр"": ""08.12.2022"",
				""ВремяСозд"": ""17:16:38"",
				""ВремяПолуч"": ""20:17:45"",
				""ДатаСозд"": ""08.12.2022""
			},
			""ДатИнфГО"": ""08.12.2022"",
			""Подписант"": {
				""СтатПодп"": ""1"",
				""ФИО"": {
					""Имя"": ""Алборишвили"",
					""Фамилия"": ""Алан"",
					""Отчество"": ""Лонгиевич""
				}
			},
			""КНД"": ""1110339"",
			""ВрИнфГО"": ""20:16:31""
		},
		""ИД_ГО"": ""2LD18a49f63-9ed4-4977-a55e-b6827fbf0eaa"",
		""ИД_ГП"": ""2LD9cd9f592-d82c-47e2-bec7-2ab57d397eb1"",
		""СодИнфГО"": {
			""СвТС"": {
				""ТС"": {
					""ТипВлад"": ""1"",
					""ПарТС"": {
						""Марка"": ""бибика"",
						""Грузопод"": ""1.00"",
						""Тип"": ""машинка"",
						""Вместим"": ""1.00""
					},
					""РегНомер"": ""12345""
				}
			},
			""НомЗак"": ""0812_03"",
			""НомерТрН"": ""0812_03"",
			""СвВодит"": [
				{
					""ДатаВыдВУ"": ""28.11.2022"",
					""ИННФЛ"": ""123456789012"",
					""СерВУ"": ""0000"",
					""НомВУ"": ""000001"",
					""ФИО"": {
						""Имя"": ""Иван"",
						""Фамилия"": ""Иванов""
					},
					""Тлф"": [
						{
							""Тлф"": ""9100000001""
						}
					]
				}
			],
			""СвГруз"": {
				""ОпГруз"": [
					{
						""Марк"": [
							{
								""Марк"": ""Марка 1""
							}
						],
						""ВидТар"": ""12"",
						""ПлМасГруз"": {
							""МасБрутЗнач"": 1
						},
						""КолМестГр"": 1,
						""НаимГруз"": ""груз"",
						""СостГруз"": ""Состояние груза"",
						""СпУпак"": ""Способ упаковки""
					}
				]
			},
			""УказГО"": {
				""СвПА"": {
					""СпосПерУкПА"": ""Электронное уведомление перевозчика о переадресовке"",
					""ЛицоПА"": ""Грузоотправитель"",
					""КонтПА"": {
						""Тлф"": [
							{
								""Тлф"": ""1234567""
							}
						]
					}
				},
				""УкНормПрвз"": ""Указания в отношении выполнения норм перевозки""
			},
			""СвГО"": {
				""РекИдентГО"": {
					""ИдСв"": {
						""СвЮЛУч"": {
							""КПП"": ""312201001"",
							""НаимОрг"": ""ООО \""ГрузоОтправитель\"""",
							""ИННЮЛ"": ""3122507629""
						}
					},
					""Контакт"": {
						""Тлф"": [
							{
								""Тлф"": ""1234567""
							}
						]
					}
				},
				""ГОЭксп"": ""0""
			},
			""СвГП"": {
				""АдресДостГр"": {
					""КодГАР"": ""99cd2450-b196-46f2-b15d-a6005b48551e""
				},
				""РекИдентГП"": {
					""ИдСв"": {
						""СвЮЛУч"": {
							""КПП"": ""312301001"",
							""НаимОрг"": ""ООО \""ГрузоПолучатель\"""",
							""ИННЮЛ"": ""7730633947""
						}
					},
					""Контакт"": {
						""Тлф"": [
							{
								""Тлф"": ""1234567""
							}
						]
					}
				}
			},
			""СодОпер"": ""Лицом, осуществляющим погрузку груза, при указанных обстоятельствах передан водителю груз с указанными характеристиками"",
			""ДатаТрН"": ""08.12.2022"",
			""СвПогруз"": {
				""ДатаФУбыт"": ""28.11.2022"",
				""НалКоорТочВрЗаяв"": ""0"",
				""СвЛицПогрГр"": {
					""ИдентРекГО"": {
						""ИННЮЛ"": ""1121289234""
					},
					""СовпГОП"": ""1""
				},
				""ДатаФПриб"": ""28.11.2022"",
				""UTCФУбыт"": ""+00:00"",
				""ВремяЗаявПогр"": ""19:00:00"",
				""НалКоорТочВрФУбыт"": ""0"",
				""КолМестПрием"": 120,
				""ФАдресПогр"": {
					""КодГАР"": ""99cd2450-b196-46f2-b15d-a6005b48551e""
				},
				""ВремяФПриб"": ""19:00:00"",
				""МасБрутОтгр"": ""10"",
				""UTCЗаявПогр"": ""+00:00"",
				""ВладИнфр"": {
					""ИдентРекГО"": {
						""ИННЮЛ"": ""1121289234""
					},
					""СовпГОВ"": ""1""
				},
				""UTCФПриб"": ""+00:00"",
				""НалКоорТочВрФПогр"": ""0"",
				""ВремяФУбыт"": ""19:00:00"",
				""МетОпрМасс"": ""01"",
				""ДатаЗаявПогр"": ""28.11.2022""
			},
			""ДатаЗак"": ""08.12.2022"",
			""СвПер"": {
				""ИдСв"": {
					""СвЮЛУч"": {
						""КПП"": ""312301001"",
						""НаимОрг"": ""ООО \""ГрузоПеревозчик\"""",
						""ИННЮЛ"": ""3123311749""
					}
				},
				""Контакт"": {
					""Тлф"": [
						{
							""Тлф"": ""1234567""
						}
					]
				}
			}
		},
		""ТипЭТРН"": ""ON_TRNACLGROT"",
		""ВерсФорм"": ""5.01"",
		""ИД_ТК"": ""2LD06bee784-7719-41f5-9c29-341d7d19a266"",
		""GUID"": ""6118fbb0-8310-48d6-a220-00375d99bf13"",
		""ИдФайл"": ""ON_TRNACLGROT_2LD06bee784-7719-41f5-9c29-341d7d19a266_2LD9cd9f592-d82c-47e2-bec7-2ab57d397eb1_2LD18a49f63-9ed4-4977-a55e-b6827fbf0eaa_0_20221213_80a5b7f5-7aef-11ed-b0d5-f15189033494"",
		""ИдФайлГИС"": ""ON_TRNACLGROT_2LD06bee784-7719-41f5-9c29-341d7d19a266_2LD9cd9f592-d82c-47e2-bec7-2ab57d397eb1_2LD18a49f63-9ed4-4977-a55e-b6827fbf0eaa_0_20221209_cc64e65b-778a-11ed-a54f-5569bf7d310e.xml"",
		""УИД_ТрН"": ""d6e5cf2c-8a5e-4095-8128-da0cab01bf1a"",
		""СодИнфПрвПрием"": {},
		""TRNACLPPRIND"": {
			""ПЭП"": {
				""ФИО"": {
					""Фамилия"": ""ИванАлан"",
					""Имя"": ""Алборишвили"",
					""Отчество"": ""Лонгиевич""
				}
			}
		}
	}
 }
}
";
 public static string fillTitul2()    //заполнение и для посылки json в строку
 {  string json2str="";
    string TIT2=ETRN_Titul2.TITUL2min;
    dynamic jsontit=JsonConvert.DeserializeObject(TIT2);
    var cetrn=jsontit.content.ЭТРН;

    json2str=JsonConvert.SerializeObject(jsontit);
    return json2str;
 }
//==========================
 } //END ETRN_Titul class

}   //END namespace
