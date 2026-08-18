#!/usr/bin/env bash
#
# add-new-module.sh - scaffold a new backend module (Clean Architecture).
#
# Creates the three .NET layer projects (Domain / Application / Infrastructure)
# for a module, wires their references, adds them to the solution, registers
# Nx project.json for each, and references Application + Infrastructure from the
# Api entry point.
#
# Usage:
#   scripts/add-new-module.sh <ModuleName>
#
# <ModuleName> is the .NET-style name used in the namespace
# (DotnetVue3TemplateRu.<ModuleName>.<Layer>). Pass it in the casing you want, e.g.:
#   scripts/add-new-module.sh Billing
#   scripts/add-new-module.sh PriceList
#
# Projects are grouped by domain: the three layers live under
# libs/backend/<kebab>/, where <kebab> is the kebab-case of the module name
# (Billing -> billing, PriceList -> price-list). The .NET project names and
# namespaces keep the PascalCase form (DotnetVue3TemplateRu.<ModuleName>.<Layer>).
#
# The kebab form is also used for Nx project names: <kebab>-domain,
# <kebab>-application, <kebab>-infrastructure.

set -euo pipefail

# --- resolve repo root (parent of this script's dir) -----------------------
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "$SCRIPT_DIR/.." && pwd)"
cd "$ROOT_DIR"

# --- args ------------------------------------------------------------------
if [ "$#" -ne 1 ] || [ -z "${1// }" ]; then
  echo "Usage: scripts/add-new-module.sh <ModuleName>" >&2
  echo "Example: scripts/add-new-module.sh Billing" >&2
  exit 1
fi

INPUT="$1"
# PascalName: uppercase first char, keep the rest as given.
PASCAL="$(tr '[:lower:]' '[:upper:]' <<< "${INPUT:0:1}")${INPUT:1}"
# Kebab slug: PriceList -> price-list, Core -> core. Used for the domain
# folder and the Nx project-name prefix.
KEBAB="$(sed -E 's/([a-z0-9])([A-Z])/\1-\2/g' <<< "$PASCAL" | tr '[:upper:]' '[:lower:]')"
NX="$KEBAB"

LIBS="libs/backend"
SLN="DotnetVue3TemplateRu.slnx"
API="apps/backend/DotnetVue3TemplateRu.Api/DotnetVue3TemplateRu.Api.csproj"
TFM="net10.0"

# Domain folder that groups the three layer projects.
MODULE_DIR="$LIBS/$KEBAB"

LAYERS=("Domain" "Application" "Infrastructure")

# --- pre-flight: refuse to overwrite an existing module --------------------
if [ -e "$MODULE_DIR" ]; then
  echo "Error: '$MODULE_DIR' already exists. Aborting (nothing changed)." >&2
  exit 1
fi

proj_path() { # <layer> -> csproj path
  echo "$MODULE_DIR/DotnetVue3TemplateRu.$PASCAL.$1/DotnetVue3TemplateRu.$PASCAL.$1.csproj"
}

# --- write the Nx project.json for one layer -------------------------------
write_project_json() { # <layer> <nx-name> <impl-dep-or-empty>
  local layer="$1" name="$2" dep="$3"
  local dir="$MODULE_DIR/DotnetVue3TemplateRu.$PASCAL.$layer"
  local csproj="DotnetVue3TemplateRu.$PASCAL.$layer.csproj"
  local deps_line=""
  if [ -n "$dep" ]; then
    deps_line="  \"implicitDependencies\": [\"$dep\"],
"
  fi
  cat > "$dir/project.json" <<EOF
{
  "name": "$name",
  "\$schema": "../../../../node_modules/nx/schemas/project-schema.json",
  "projectType": "library",
$deps_line  "targets": {
    "build": {
      "executor": "nx:run-commands",
      "dependsOn": ["^build"],
      "options": {
        "command": "dotnet build $dir/$csproj -c Debug --no-dependencies"
      }
    }
  }
}
EOF
}

echo ">> Creating module '$PASCAL' (Nx prefix: '$NX')"

# --- 1. create the three classlib projects ---------------------------------
for layer in "${LAYERS[@]}"; do
  dir="$MODULE_DIR/DotnetVue3TemplateRu.$PASCAL.$layer"
  echo ">> dotnet new classlib -> $dir"
  dotnet new classlib -n "DotnetVue3TemplateRu.$PASCAL.$layer" -o "$dir" -f "$TFM"
  rm -f "$dir/Class1.cs"
done

# --- 2. layer references: Application -> Domain, Infrastructure -> Application
echo ">> wiring layer references"
dotnet add "$(proj_path Application)"    reference "$(proj_path Domain)"
dotnet add "$(proj_path Infrastructure)" reference "$(proj_path Application)"

# --- 3. add projects to the solution ---------------------------------------
echo ">> adding projects to $SLN"
dotnet sln "$SLN" add \
  "$(proj_path Domain)" \
  "$(proj_path Application)" \
  "$(proj_path Infrastructure)"

# --- 4. reference Application + Infrastructure from the Api ------------------
echo ">> referencing module from Api"
dotnet add "$API" reference \
  "$(proj_path Application)" \
  "$(proj_path Infrastructure)"

# --- 5. Nx project.json per layer ------------------------------------------
echo ">> writing Nx project.json files"
write_project_json Domain         "$NX-domain"         ""
write_project_json Application    "$NX-application"    "$NX-domain"
write_project_json Infrastructure "$NX-infrastructure" "$NX-application"

cat <<EOF

Done. Module '$PASCAL' created:
  $MODULE_DIR/DotnetVue3TemplateRu.$PASCAL.Domain          (nx: $NX-domain)
  $MODULE_DIR/DotnetVue3TemplateRu.$PASCAL.Application      (nx: $NX-application)
  $MODULE_DIR/DotnetVue3TemplateRu.$PASCAL.Infrastructure   (nx: $NX-infrastructure)

Next steps (manual):
  1. Register the module's services/DbContext in the Api DI (Program.cs),
     as done for the Core module.
  2. Verify:
       dotnet build $SLN
       yarn nx graph
EOF
