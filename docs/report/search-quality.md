# Якість пошуку

Повний датасет (20 300 документів: 20 000 статей Вікіпедії і 300 тестових), `top_k = 3`. «Без» — `expand: false`, «З» — з розширенням запиту (`gemma4:e2b`).

| Запит | Очікувана тема | Топ-3 без розширення | Топ-3 з розширенням | Додані терміни |
|---|---|---|---|---|
| tallest mountain | найвищі гори | Billings, Montana; Tourism in Tamil Nadu; Forests Commission Victoria | Olympic Mountains; Mount Everest; Grand Teton National Park | highest, peak, summit, alpine, elevation, climax, zenith |
| world war two battles | битви Другої світової | Naval warfare; Battle of the Somme; Bhishma Parva | Military history; Naval warfare; Military history of Japan | conflict, battle, combat, history, military, fighting, era |
| famous painter | відомі художники | List of people from Italy; List of people from Southern Italy; List of Welsh people | List of people from Italy; James McNeill Whistler; List of people from Southern Italy | artist, master, creator, figure, icon, genius, masterpiece |
| heart disease | хвороби серця | Outline of cardiology; Cardiovascular disease in women; Atrial fibrillation | Outline of cardiology; Cardiovascular disease in women; Myocardial infarction | cardiac, illness, condition, myocardial, arrhythmia, stroke |
| solar system planets | планети Сонячної системи | Formation and evolution of the Solar System; History of Solar System formation and evolution hypotheses; Jumping-Jupiter scenario | Timeline of Solar System astronomy; Heliocentrism; Astronomy | celestial, worlds, orbit, astronomy, spheres |
| football world cup | футбол, чемпіонат світу | Timeline of association football; Timeline of Scottish football; Double (association football) | Football in South America; List of 2023 FIFA Women's World Cup controversies; Sky Sports | soccer, tournament, match, competition, global, sport, event, game |
| electric car | електромобілі | Plug-in electric vehicles in Norway; Plug-in electric vehicle; Electric car use by country | Electric vehicle charging network; Government incentives for plug-in electric vehicles; Electric car use by country | ev, automobile, vehicle, battery, transport, mobility |
| ancient roman emperor | римські імператори | Succession to the Byzantine Empire; Culture of ancient Rome; Roman Cyprus | History of the Roman Empire; Roman Cyprus; Imperial Roman army | caesar, augustus, trajan, hadrian, nero, antonine, imperial |
| jazz musician | джазові музиканти | List of Canadian musicians; Bix Beiderbecke; Al (given name) | List of Canadian musicians; Timeline of music in the United States (1920–1949); Glossary of jazz and popular music | performer, artist, improviser, swing, rhythm, sound |
| programming language | мови програмування | List of programming languages by type; Computer program; List of United States over-the-air television networks | C++; Computer program; C++11 | code, script, syntax, software, compilation, algorithm, development |

Розширення додає синоніми й пов'язані слова та часто прибирає випадкові збіги (tallest mountain: замість статей про місто й туризм — Mount Everest; programming language: зникає список телемереж), але інколи зсуває результат убік (football world cup — Sky Sports).
Пошук без розширення займає 1–6 мс, з розширенням близько 0,8–1,2 с після завантаження моделі; перший запит після старту ≈ 15–20 с. Ollama обмежено продуктивними ядрами (`OLLAMA_NUM_THREAD=4`).
