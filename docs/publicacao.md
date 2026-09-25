# Publicação: como o .exe chega ao cliente

O MT Mapa de Rede não tem servidor. Publicar uma versão é gerar o `mt-mapa-rede.exe` a partir do `main` e entregá-lo a quem vai usar. A entrega é sempre com autorização do Manfred.

## 1. Gerar o .exe

Duas formas, que dão o mesmo resultado:

- **Pelo CI:** todo push no `main` e todo Pull Request rodam o GitHub Actions, que compila, testa e guarda o `.exe` por 14 dias. No GitHub, abra **Actions**, a execução do commit e, no fim da página, **Artifacts**.
- **Na máquina do Manfred:** na pasta do projeto, rodar `ferramentas\publicar.cmd`. O roteiro roda os testes e grava o `.exe` em `publicar\`.

## 2. Antes de publicar

1. O Pull Request da fatia foi juntado ao `main` com a frase de autorização.
2. O CI do `main` passou, conferido na tela do Actions.
3. A versão em `Directory.Build.props` (`<Version>`) foi aumentada no mesmo Pull Request.
4. O `.exe` foi testado na rede da MT, pela janela e pela linha de comando.
5. A tabela OUI foi atualizada há menos de seis meses (`ferramentas\atualizar-oui.ps1`).

## 3. Publicar

1. Criar a marca da versão no `main`: `git tag v0.1.0` e `git push origin v0.1.0`.
2. Guardar o `.exe` e o código SHA-256 dele (`certutil -hashfile mt-mapa-rede.exe SHA256`) na pasta de versões da MT, fora do repositório.
3. Copiar o `.exe` para o pendrive ou a pasta de ferramentas do técnico.

## 4. Depois de publicar

- Atualizar a "Situação do projeto" do README.
- Anotar em `docs/superpowers/pendencias.md` o que o uso em campo mostrar.

## 5. Quando houver página de download

Se o `.exe` passar a ser baixado de uma página da MT, a publicação dessa página segue `docs/metodo/roteiro-publicacao-git-cpanel.md`, com decisão e autorização do Manfred.
