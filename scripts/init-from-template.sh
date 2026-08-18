#!/usr/bin/env bash
#
# init-from-template.sh - превратить копию шаблона в новый проект.
#
# Делает три вещи и удаляет себя:
#   1. переименовывает каталоги и файлы, в именах которых есть имя шаблона;
#   2. заменяет имя шаблона в содержимом всех файлов (три формы: PascalCase,
#      kebab-case, camelCase);
#   3. сдвигает все внешние порты на заданное смещение, чтобы новый проект
#      поднимался одновременно с другими проектами на том же стеке.
#
# Usage:
#   scripts/init-from-template.sh --name AcmeCrm --port-offset 100
#   scripts/init-from-template.sh --name AcmeCrm --port-offset 100 --kebab acme-crm
#
# --name         PascalCase-имя проекта: ^[A-Z][A-Za-z0-9]*$, без точек.
#                Из него выводятся namespace, имена проектов и каталогов.
# --kebab        kebab-форма, если автоматический вывод не устраивает.
#                По умолчанию AcmeCrm -> acme-crm.
# --port-offset  Целое 0..40000, прибавляется к каждому внешнему порту.
#                Обязателен и без значения по умолчанию: два проекта на этом
#                стеке с одинаковыми портами одновременно не поднимутся.
# --keep-script  Не удалять скрипт и не накладывать overlay. Нужно только для
#                сопровождения самого шаблона, а не для генерации проекта.

set -euo pipefail

# --- имя шаблона в трёх формах -------------------------------------------
# Эти три значения переписывает сам скрипт, когда его прогоняют по шаблону
# с --keep-script. В сгенерированном проекте скрипта уже нет.
TPL_PASCAL="DotnetVue3TemplateRu"
TPL_KEBAB="dotnet-vue3-template-ru"
TPL_CAMEL="dotnetVue3TemplateRu"

# --- внешние порты ---------------------------------------------------------
# Сдвигаются все. Внутренние порты контейнеров (3001/3002 Logto, 1025/8025
# Mailpit, 8080 в Dockerfile, 5432 PostgreSQL) не трогаются: они живут внутри
# сети Docker и между проектами не конфликтуют.
PORTS=(5173 3481 3482 1425 8425 5249 7324 18181 16197 21316 22120 6006)

# Каталоги, которые не обходим ни на одном шаге.
PRUNE=(.git node_modules bin obj .nx .yarn dist .aspire coverage storybook-static)

# Файлы, которые нужны только самому шаблону. В сгенерированном проекте они
# описывали бы то, чего в нём уже нет, поэтому удаляются вместе со скриптом.
TEMPLATE_ONLY=(docs/guides/create-project-from-template.md)

SCRIPT_REL="scripts/init-from-template.sh"

# --- разбор аргументов -----------------------------------------------------
NAME=""
KEBAB=""
OFFSET=""
KEEP_SCRIPT=0

die() { echo "Ошибка: $*" >&2; exit 1; }

usage() {
  sed -n '3,26p' "$0" | sed 's/^# \{0,1\}//'
  exit "${1:-0}"
}

while [ $# -gt 0 ]; do
  case "$1" in
    --name)        NAME="${2:-}"; shift 2 ;;
    --kebab)       KEBAB="${2:-}"; shift 2 ;;
    --port-offset) OFFSET="${2:-}"; shift 2 ;;
    --keep-script) KEEP_SCRIPT=1; shift ;;
    -h|--help)     usage 0 ;;
    *)             echo "Неизвестный аргумент: $1" >&2; usage 1 ;;
  esac
done

[ -n "$NAME" ]   || { echo "Не задан --name" >&2; usage 1; }
[ -n "$OFFSET" ] || { echo "Не задан --port-offset" >&2; usage 1; }

case "$NAME" in
  [A-Z]*) ;;
  *) die "--name должен начинаться с заглавной буквы: '$NAME'" ;;
esac
printf '%s' "$NAME" | grep -qE '^[A-Z][A-Za-z0-9]*$' \
  || die "--name допускает только латинские буквы и цифры, без точек и дефисов: '$NAME'"

printf '%s' "$OFFSET" | grep -qE '^[0-9]+$' \
  || die "--port-offset должен быть целым неотрицательным числом: '$OFFSET'"
[ "$OFFSET" -le 40000 ] || die "--port-offset слишком велик (максимум 40000): $OFFSET"

# kebab по умолчанию: AcmeCrm -> acme-crm, DotnetVue3TemplateRu -> dotnet-vue3-template-ru
if [ -z "$KEBAB" ]; then
  KEBAB="$(printf '%s' "$NAME" | sed -E 's/([a-z0-9])([A-Z])/\1-\2/g' | tr '[:upper:]' '[:lower:]')"
fi
printf '%s' "$KEBAB" | grep -qE '^[a-z0-9]+(-[a-z0-9]+)*$' \
  || die "--kebab должен быть строчным с дефисами: '$KEBAB'"

# camel: первая буква PascalCase в нижний регистр
CAMEL="$(printf '%s' "$NAME" | sed -E 's/^(.)/\l\1/')"

# --- предполётные проверки -------------------------------------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT_DIR"

[ -f "$TPL_PASCAL.slnx" ] \
  || die "не найден $TPL_PASCAL.slnx - запускать скрипт нужно из корня копии шаблона."

if [ "$NAME" = "$TPL_PASCAL" ]; then
  die "новое имя совпадает с именем шаблона - менять нечего."
fi

# Собираем список путей, которые обходим: один раз, дальше переиспользуем.
prune_expr=()
for d in "${PRUNE[@]}"; do
  prune_expr+=(-name "$d" -o)
done
unset 'prune_expr[${#prune_expr[@]}-1]'

echo ">> Проект:        $NAME"
echo ">> kebab-форма:   $KEBAB"
echo ">> camel-форма:   $CAMEL"
echo ">> Сдвиг портов:  +$OFFSET"
echo

# --- 1. переименование каталогов и файлов ----------------------------------
# -depth обрабатывает содержимое раньше контейнера, поэтому переименование
# вложенного пути не ломает ещё не пройденный родительский.
rename_pass() { # <откуда> <куда>
  local from="$1" to="$2" p base new
  find . \( "${prune_expr[@]}" \) -prune -o -depth -name "*${from}*" -print \
  | while IFS= read -r p; do
      base="$(basename "$p")"
      new="$(dirname "$p")/${base//$from/$to}"
      [ "$p" = "$new" ] && continue
      mv "$p" "$new"
      echo "   $p -> $new"
    done
}

echo ">> Переименование каталогов и файлов"
rename_pass "$TPL_PASCAL" "$NAME"
rename_pass "$TPL_KEBAB" "$KEBAB"
echo

# --- 2. замена имени в содержимом ------------------------------------------
# grep -I пропускает бинарные файлы. Порядок форм важен: kebab и camel не
# являются подстроками Pascal, поэтому пересечений между заменами нет.
echo ">> Замена имени в содержимом файлов"
files_with_name() {
  grep -rIl --null \
    --exclude-dir=.git --exclude-dir=node_modules --exclude-dir=bin \
    --exclude-dir=obj --exclude-dir=.nx --exclude-dir=.yarn --exclude-dir=dist \
    --exclude-dir=.aspire --exclude-dir=coverage --exclude-dir=storybook-static \
    -e "$TPL_PASCAL" -e "$TPL_KEBAB" -e "$TPL_CAMEL" . 2>/dev/null || true
}
count="$(files_with_name | tr '\0' '\n' | grep -c . || true)"
files_with_name | xargs -0 -r sed -i \
  -e "s/${TPL_PASCAL}/${NAME}/g" \
  -e "s/${TPL_KEBAB}/${KEBAB}/g" \
  -e "s/${TPL_CAMEL}/${CAMEL}/g"
echo "   файлов изменено: $count"
echo

# --- 3. сдвиг портов -------------------------------------------------------
# yarn.lock исключён намеренно: числа портов встречаются внутри контрольных
# сумм пакетов, и замена там испортила бы lock-файл.
if [ "$OFFSET" -gt 0 ]; then
  echo ">> Сдвиг портов на +$OFFSET"
  port_files() {
    grep -rIl --null \
      --exclude-dir=.git --exclude-dir=node_modules --exclude-dir=bin \
      --exclude-dir=obj --exclude-dir=.nx --exclude-dir=.yarn --exclude-dir=dist \
      --exclude-dir=.aspire --exclude-dir=coverage --exclude-dir=storybook-static \
      --exclude=yarn.lock --exclude="$(basename "$SCRIPT_REL")" \
      -E -e "$(IFS='|'; echo "${PORTS[*]}")" . 2>/dev/null || true
  }
  # Двухфазная замена через метки: иначе сдвинутый порт мог бы совпасть с
  # ещё не обработанным исходным и попасть под замену второй раз.
  sed_to_marker=()
  sed_from_marker=()
  for i in "${!PORTS[@]}"; do
    old="${PORTS[$i]}"
    new=$((old + OFFSET))
    [ "$new" -lt 65536 ] || die "порт $old со сдвигом +$OFFSET выходит за 65535."
    sed_to_marker+=(-e "s/\b${old}\b/@@P${i}@@/g")
    sed_from_marker+=(-e "s/@@P${i}@@/${new}/g")
    echo "   $old -> $new"
  done
  port_files | xargs -0 -r sed -i "${sed_to_marker[@]}"
  port_files() { # после первой фазы искать надо метки
    grep -rIl --null \
      --exclude-dir=.git --exclude-dir=node_modules --exclude-dir=bin \
      --exclude-dir=obj --exclude-dir=.nx --exclude-dir=.yarn --exclude-dir=dist \
      --exclude-dir=.aspire --exclude-dir=coverage --exclude-dir=storybook-static \
      --exclude=yarn.lock \
      -E -e '@@P[0-9]+@@' . 2>/dev/null || true
  }
  port_files | xargs -0 -r sed -i "${sed_from_marker[@]}"
  echo
else
  echo ">> Сдвиг портов пропущен (--port-offset 0)"
  echo
fi

# --- 4. overlay и самоудаление ---------------------------------------------
if [ "$KEEP_SCRIPT" -eq 0 ]; then
  if [ -d template-overlay ]; then
    echo ">> Наложение template-overlay/ (файлы, которые в проекте выглядят иначе, чем в шаблоне)"
    find template-overlay -type f | sed 's|^template-overlay/|   |'
    cp -R template-overlay/. .
    rm -rf template-overlay
    echo
  fi
  for f in "${TEMPLATE_ONLY[@]}"; do
    if [ -e "$f" ]; then
      rm -f "$f"
      echo ">> Удалён файл, нужный только шаблону: $f"
    fi
  done
  rm -f "$SCRIPT_REL"
fi

cat <<EOF
Готово. Дальше:

  yarn install
  dotnet build $NAME.slnx
  yarn dev

Проверить, что имя шаблона нигде не осталось:

  grep -rI "$TPL_PASCAL\|$TPL_KEBAB" . --exclude-dir=node_modules --exclude-dir=.git

Первый коммит делайте после того, как сборка прошла: так в истории не окажется
промежуточного состояния с наполовину переименованными проектами.
EOF
