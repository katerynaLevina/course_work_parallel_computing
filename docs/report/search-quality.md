# Якість пошуку

Повний датасет (495 846 документів), `top_k = 3`. «Без» — `expand: false`, «З» — з розширенням запиту (`gemma4:e2b`).

| Запит | Очікувана тема | Топ-3 без розширення | Топ-3 з розширенням | Додані терміни |
|---|---|---|---|---|
| tallest mountain | найвищі гори | Central Mountain Range; List of tallest structures in Hungary; Big Bald Mountain | Rich Mountain (Georgia); Snake Range; Big Bald Mountain | highest, peak, summit, alpine, elevation, climax, zenith |
| world war two battles | битви Другої світової | Battle of Sunchon (air); Battle; Japan campaign | Battle; Close combat; List of SPI games | conflict, battle, combat, history, military, fighting, era |
| famous painter | відомі художники | Kamalolmolk (film); Polygnotos (vase painter); Pieter Post | Juan Bautista Martínez del Mazo; Brygos Painter; Max Shertz | artist, master, creator, figure, icon, genius, masterpiece |
| heart disease | хвороби серця | National Wear Red Day; Coronary artery disease; World Heart Federation | Cardiology; Ultrasound-enhanced systemic thrombolysis; List of MeSH codes (C14) | cardiac, illness, condition, myocardial, arrhythmia, stroke |
| solar system planets | планети Сонячної системи | Small Solar System body; Formation and evolution of the Solar System; Hypothetical astronomical object | IAU definition of planet; Clearing the neighbourhood; Hypothetical astronomical object | celestial, worlds, orbit, astronomy, spheres |
| football world cup | футбол, чемпіонат світу | Timeline of association football; Vanuatu Football Federation; Thailand women's national football team | Powerchair Football; Sports table football; Soccer in Australia | soccer, tournament, match, competition, global, sport, event, game |
| electric car | електромобілі | Wally Rippel; Bushbury Electric; Chelsea Sexton | MIEV; Heibao Auto; Who Killed the Electric Car? | ev, automobile, vehicle, battery, transport, mobility |
| ancient roman emperor | римські імператори | Daco-Roman; Philip II (Roman emperor); Marcianopolis | Marcus Ulpius Traianus (father of Trajan); Matidia Minor; Julii Caesares | caesar, augustus, trajan, hadrian, nero, antonine, imperial |
| jazz musician | джазові музиканти | Chamber jazz; Chase Sanborn; Eva Kurowski | Jazz improvisation; Gypsy jazz; Hank Penny | performer, artist, improviser, swing, rhythm, sound |
| programming language | мови програмування | Outline of computer programming; The C++ Programming Language; Little b (programming language) | Outline of computer programming; Compiler; Computer programming | code, script, syntax, software, compilation, algorithm, development |

Розширення додає синоніми й пов'язані слова та часто підіймає тематично точніші статті (heart disease, jazz musician, ancient roman emperor), але інколи зсуває результат убік (football world cup).
Пошук без розширення займає 16–2000 мс, з розширенням близько 1 с (0,9–1,8 с для «ocean») після завантаження моделі; перший запит після старту ≈ 15–20 с. Ollama обмежено продуктивними ядрами (`OLLAMA_NUM_THREAD=4`).
