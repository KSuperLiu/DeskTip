"""2026 official calendar, kept separate from the generic reminder engine."""
import datetime, json
from pathlib import Path
root = Path(__file__).resolve().parent.parent
overrides = {}
for start, end in [('01-01','01-03'), ('02-15','02-23'), ('04-04','04-06'), ('05-01','05-05'), ('06-19','06-21'), ('09-25','09-27'), ('10-01','10-07')]:
    day = datetime.date.fromisoformat('2026-' + start)
    last = datetime.date.fromisoformat('2026-' + end)
    while day <= last:
        overrides[day.isoformat()] = False
        day += datetime.timedelta(days=1)
for day in ['01-04','02-14','02-28','05-09','09-20','10-10']:
    overrides['2026-' + day] = True
path = root / 'src/DeskTip/Assets/calendar-2026.json'
path.parent.mkdir(parents=True, exist_ok=True)
path.write_text(json.dumps({'Years':[2026], 'Overrides':dict(sorted(overrides.items())), 'Source':'国务院办公厅国办发明电〔2025〕7号 https://www.beijing.gov.cn/cs/gncs/zcwj/202603/t20260327_4568275.html'}, ensure_ascii=False, indent=2), encoding='utf-8')
print(f'{len(overrides)} calendar overrides written')
