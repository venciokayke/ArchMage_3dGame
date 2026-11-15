# IA dos inimigos

Unity 2022.3.62f3. Continua usando o NavMesh existente: NavMeshAgent calcula caminhos/desvio, CharacterController aplica deslocamento/colisao e devolve a posicao ao agente. Root motion e rotacao automatica do agente continuam desligados. ArenaNavigation, WaveManager, cinco ondas, spawns, jogador, camera e HUD nao foram alterados.

## Aplicar na partida existente

1. Abra `Assets/Scenes/UnderdogSurvival.unity`, fora do Play Mode. Confira os tres prefabs ja referenciados pelo WaveManager.
2. Execute **Tools > Underdog > Update Enemy AI Presets**. Confira os caminhos mostrados e confirme. Nao execute **Prepare Survival Game** para atualizar apenas IA: esse outro comando recria a partida.
3. O comando cria `Assets/Generated/EnemyAI_<data>/`: `Before_0/1/2.prefab` sao backups Warrior/Rogue/Mage; os Controllers copiados passam a ser usados pelos respectivos prefabs. Os `.meta`, GUIDs, modelos, armas, projeteis, clips e eventos existentes sao preservados. A cena nao precisa ser regravada.
4. Entre em Play Mode e inicie a partida normalmente. Overrides de componentes em instancias colocadas manualmente na cena podem prevalecer sobre o prefab; confira-os sem aplicar/reverter outros overrides indiscriminadamente.

O menu exige um unico WaveManager na cena ativa, tres prefabs distintos de gameplay com Vida/CharacterController/NavMeshAgent e um AnimatorController com estado `Locomotion` na Base Layer. Ele recusa assets em `Assets_Externos`, prefabs fora da raiz, Controllers incompativeis e alteracoes nao salvas nos assets envolvidos/Prefab Mode, antes de alterar arquivos. Salve seu trabalho primeiro. Nao ha necessidade de rebake: nenhuma geometria nem dimensao dos agentes foi modificada.

O comando reaplica valores iniciais: se ja tiver balanceado os parametros, registre-os antes de executar novamente. Cada execucao confirmada cria um backup separado. Para reverter, feche Play Mode, restaure os prefabs anteriores com o controle de versao ou com os backups, mantendo os `.meta` originais; nao arraste os backups para os slots do WaveManager.

## Comportamentos

| Arquetipo | Valores iniciais | Comportamento |
| --- | --- | --- |
| Skeleton Warrior | 175 HP, velocidade 2.5 | Pressiona diretamente; reserva uma posicao proxima do jogador. Se o anel interno esta ocupado, aguarda em um anel externo e procura vaga periodicamente. |
| Skeleton Rogue | 60 HP, velocidade 5.5 | Aproxima por um waypoint lateral valido, mantendo o lado escolhido durante a manobra, depois busca alcance melee. Reinicia o flanco quando o jogador se afasta bastante. |
| Skeleton Mage | 75 HP, velocidade 2.6 | Busca raio 7.5, recua abaixo de 6 e usa margem 0.8 para sair do recuo. Aproxima quando distante; procura linha de tiro e pode deslocar lateralmente. Candidatos invalidos junto das paredes sao descartados. |

O alcance de ataque existente do Mage (8) continua limitando seus disparos, dentro da faixa tatica 6-9. Dano e cooldowns de todos os ataques continuam existentes. Hit nao cancela um ataque em andamento; Attack pausa deslocamento/reposicionamento ate concluir. Death cancela golpes e retira imediatamente o inimigo das reservas/separacao, desativando agente e colliders.

Os destinos sao distribuidos em aneis e mantidos ate chegada, mudanca significativa do alvo, conflito ou renovacao da manobra. Decisoes sao escalonadas e separacao e calculada apenas nesse intervalo. Caminhos completos sao exigidos antes de aceitar um destino; nenhuma posicao e aplicada por teleporte. HighQuality avoidance e prioridades distintas complementam a separacao local.

## Parametros no Inspector (EnemyAI)

| Parametro | Inicial | Uso |
| --- | --- | --- |
| `comportamento` | explicito por prefab | Automatico permite compatibilidade com prefabs antigos: ranged pelo ataque, Rogue pela velocidade >= 3.5. |
| `velocidade` / `Vida.vidaMaxima` | por arquetipo | Velocidade e resistencia. |
| `intervaloDecisao` | 0.35 s | Atualiza estado, separacao e necessidade de rota; limite de runtime 0.15 s. |
| `intervaloReposicionamento` | 2.4 s | Tempo minimo para renovar manobra; viagens longas recebem prazo maior. |
| `espacamento` | 0.9 | Espaco entre destinos reservados. |
| `raioSeparacao` / `forcaSeparacao` | 1.2 / 0.7 | Afastamento suave de vizinhos vivos com o mesmo alvo. |
| `raioFlanco` / `anguloFlanco` | 4 / 65 graus | Waypoint lateral do Rogue. |
| `distanciaMinima` / `distanciaIdeal` / `distanciaMaxima` | 6 / 7.5 / 9 | Faixa do Mage; alcance de ataque ainda precisa permitir o disparo. |
| `margemDistancia` | 0.8 | Histerese do recuo. |
| `velocidadeReferenciaAnimacao` | 2.5 | Velocidade de deslocamento correspondente ao ritmo normal do clip de corrida. |

O runtime mede o deslocamento real do CharacterController. `Speed` continua controlando o Blend Tree; `LocomotionRate` ajusta apenas o ritmo do estado Locomotion nos Controllers copiados. Os estados Attack, Hit e Death mantem suas velocidades. Nao aumente raio de separacao/espacamento muito alem do alcance melee: isso reduz vagas de ataque.

## Validacao e limites

- **Compilacao estatica:** todos os scripts de runtime/Editor compilados contra os assemblies da Unity 2022.3.62f3. Avisos preexistentes de NavigationStatic e ItemEffect permanecem.
- **Play Mode automatizado, cena sintetica isolada:** um guerreiro; oito melee do mesmo lado; flanco e aproximacao final; ranged na faixa ideal, borda e cobertura; 25 mistos; deslocamento do alvo como dash; morte liberando reservas. Tambem alcance, pausa durante ataque, evento unico, cooldown e cancelamento por morte. XML em `Docs/Validation/EnemyAI-PlayMode.xml`.
- **Edit Mode automatizado:** perfis e backups via APIs reais do Editor em prefabs sinteticos; GUIDs e Controller de origem preservados; evento/Attack/Hit/Death mantidos; slots duplicados rejeitados. XML em `Docs/Validation/EnemyAI-Presets-EditMode.xml`.
- **Regressao existente:** os mesmos relatorios incluem vida/invulnerabilidade/morte, mira XZ/cajado/evento/cancelamento do jogador, projeteis com passos 60/10 FPS, area sem dano duplicado ou atraves de parede, cinco ondas vazias concluindo em vitoria e derrota por morte. Nao equivale a jogar as cinco ondas completas contra os modelos reais.
- Os menus **Check Enemy Navigation (Play Mode)** e **Check Enemy AI Presets (Edit Mode)** repetem essas verificacoes. Prefira uma cena vazia para a suite de navegacao: ela cria atores e NavMesh temporarios perto de (200, 0, 200) e os remove ao terminar. A fixture deve ficar distante de outros objetos da cena.
- A suite headless usa passo simulado 1/60 s para nao confundir quantizacao numerica de deslocamentos minimos com falha de navegacao. Os contadores de decisoes/caminhos nao medem custo de CPU. A busca de vizinhos e linear por inimigo, adequada ao alvo de 15-25; centenas exigiriam medicao e possivelmente particionamento espacial.

**Pendentes no Editor interativo, com os modelos reais e a arena aprovada:**

1. Validar 1 inimigo de cada tipo e depois grupos mistos: Warrior pressionando, Rogue chegando lateralmente e Mage disparando em distancia favoravel.
2. Atrair varios melee pelo mesmo lado; verificar ocupacao de vagas, ausencia de sobreposicao persistente e espaco para escapar.
3. Aproximar de Magos e empurra-los contra bordas/colunas: devem escolher alternativa valida, sem tremer ou insistir fora da malha.
4. Usar paredes/obstaculos como cobertura; confirmar que Mage procura tiro e todos contornam os obstaculos.
5. Usar dash real e as tres magias no grupo; confirmar alcance/cooldowns, projeteis, ausencia de atravessamento e preservacao de Hit/Death.
6. Observar o ritmo de corrida, a pausa durante Attack e Death unica. Ajustar apenas velocidadeReferenciaAnimacao se o clip parecer deslizar.
7. Concluir as cinco ondas e testar derrota/reinicio. Quantidades e regras continuam iguais; resistencia/velocidade novas podem mudar a dificuldade percebida.
8. Usar Unity Profiler com 15-25 inimigos simultaneos para medir AI/Physics/NavMesh no hardware de destino. Nao foi realizado benchmark grafico nem teste humano de jogabilidade.
