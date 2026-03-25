import os

# Настройки
PROJECT_ROOT = "."  # Путь к твоему решению (точка означает текущую папку)
OUTPUT_FILE = "full_project_code.txt"
EXCLUDE_DIRS = {'.vs', 'bin', 'obj', '.git', 'Lib', 'References'} # Что игнорируем
EXTENSIONS = {'.cs', '.csproj', '.sln'} # Какие файлы собираем

def collect_code():
    with open(OUTPUT_FILE, 'w', encoding='utf-8') as out:
        out.write("=== СТРУКТУРА ПРОЕКТА ===\n")
        
        # 1. Генерируем дерево папок
        for root, dirs, files in os.walk(PROJECT_ROOT):
            dirs[:] = [d for d in dirs if d not in EXCLUDE_DIRS]
            level = root.replace(PROJECT_ROOT, '').count(os.sep)
            indent = ' ' * 4 * level
            out.write(f'{indent}{os.path.basename(root)}/\n')
            sub_indent = ' ' * 4 * (level + 1)
            for f in files:
                if any(f.endswith(ext) for ext in EXTENSIONS):
                    out.write(f'{sub_indent}{f}\n')

        out.write("\n\n=== СОДЕРЖИМОЕ ФАЙЛОВ ===\n")

        # 2. Собираем код
        for root, dirs, files in os.walk(PROJECT_ROOT):
            dirs[:] = [d for d in dirs if d not in EXCLUDE_DIRS]
            for file in files:
                if any(file.endswith(ext) for ext in EXTENSIONS):
                    file_path = os.path.join(root, file)
                    relative_path = os.path.relpath(file_path, PROJECT_ROOT)
                    
                    out.write(f"\n\n{'='*50}\n")
                    out.write(f"ФАЙЛ: {relative_path}\n")
                    out.write(f"{'='*50}\n\n")
                    
                    try:
                        with open(file_path, 'r', encoding='utf-8') as f:
                            out.write(f.read())
                    except Exception as e:
                        out.write(f"Ошибка чтения файла: {e}\n")

    print(f"Готово! Весь проект собран в файл: {OUTPUT_FILE}")

if __name__ == "__main__":
    collect_code()