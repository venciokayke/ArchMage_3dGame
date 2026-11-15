# Underdog 3D — checkpoints de desenvolvimento

## Como retomar

Leia este arquivo antes de implementar. Consulte git status e git log; preserve trabalho local. Nao refaca checkpoints concluidos. Nao execute Prepare Survival Game para aplicar apenas IA: ele recria a partida. Camera, arena, HUD, habilidades, vida/dano e WaveManager estao fora do escopo desta etapa.

## CP0 — baseline e analise (2026-11-15)

Estado inicial: branch mage, HEAD d966bbb. A sobrevivencia aprovada ainda estava em arquivos locais modificados/novos: runtime, assets gerados em Assets/Generated/UnderdogSurvival, cena UnderdogSurvival, pacotes, Build Settings e relatorios anteriores. Sera criado um commit local de baseline com esses arquivos antes dos commits de IA; nenhum descarte ou push.

Analise concluida:
- EnemyAI decide perseguir/atacar por distancia e reenvia o destino do jogador a cada 0.2 s. Ranged apenas usa um ponto atras de si para recuar.
- Todos os agentes partilham destino e prioridade padrao; nao ha reserva de posicoes, separacao local ou identidade de flanco. Isso favorece filas e congestionamento.
- Agent usa updatePosition=false e updateRotation=false; CharacterController.Move aplica desiredVelocity e gravidade; agent.nextPosition e sincronizado ao fim do frame. Preservar essa divisao.
- ArenaNavigation apenas carrega/remove NavMeshData; WaveManager controla as cinco ondas e spawns. Nenhum dos dois precisa mudar.
- Prefabs gerados: Melee (100 HP/2.5), Fast (60 HP/4), Ranged (75 HP/2.6), avoidance HighQuality. Os novos valores solicitados serao aplicados por comando seguro de Editor aos prefabs referenciados pelo manager, sem regravar cena ou ondas.

Proximas etapas:
1. CP1: destinos validos distribuidos, separacao local e atualizacoes escalonadas; preservar ataques/animacoes e validar compilacao.
2. CP2: flanqueamento e ranged com histerese/linha de tiro; comando seguro de presets e compilacao.
3. CP3: testes automatizados de navegacao/grupos/combate, regressao das ondas inalteradas, instrucoes e commit final.

Testes pendentes: perseguicao individual, melee em grupo, flanco, ranged em paredes/cobertura, grupo misto, dash do jogador e 15–25 agentes. Compilar nao significa validar gameplay ou visual.

## CP1 — runtime de posicionamento (em validacao)

Baseline confirmado: 4011aaf (sobrevivencia aprovada e CP0 ja registrados; nao houve necessidade de duplicar o commit).
Arquivos em trabalho: EnemyAI.cs e EnemyNavigationChecks.cs (+ meta).
Implementado: destinos reservados em aneis, separacao local no mesmo grupo/alvo, prioridades de avoidance distintas, decisoes escalonadas com intervalo minimo, limite de caminhos por decisao, parametros do Animator em cache e velocidade medida do CharacterController. Ataques continuam por seus eventos/timers existentes e pausam a navegacao sem cancelar animacoes. Rogue usa waypoint lateral antes de entrar no anel; Mage usa faixa ideal, histerese e procura posicoes com LOS no NavMesh.
Compilacao passou contra Unity 2022.3.62f3. Avisos anteriores: NavigationStatic e ItemEffect.
Falta antes de concluir CP1: suite real de Play Mode (individual, grupos melee, flanco, ranged/bordas/cobertura, grupo misto 25, dash e morte). Ajustar somente falhas observadas. Nenhum arquivo de ondas, arena, camera ou HUD foi alterado.
Depois: CP2 presets seguros para os prefabs existentes e ajuste somente da taxa da locomocao nos Controllers gerados; CP3 regressao/relatorios/instrucoes.
Validacao parcial CP1: individuo, oito melee, flanco, faixa ranged e recuo na borda passaram no runner Unity. Cenário com parede/LOS falhou; diagnostico de posicao/rota adicionado, investigacao pendente. Corrigida tambem inicializacao nativa de NavMeshPath para Start (Unity nao permite no inicializador de MonoBehaviour). Grupo 25 ainda nao executado ate superar LOS. Nao marcar CP1 como concluido antes da suite completa.

### CP1 concluido — navegacao e comportamentos

Validacao final em Unity 2022.3.62f3, projeto isolado, runner PlayMode com passo simulado de 1/60 s: PASS. A falha parcial de deslocamento vinha da fixture a 5000 unidades com passos diminutos no batch headless; corrigida a fixture para 200 unidades e o passo do runner, sem mudar a taxa do jogo. Parede/LOS tambem passou. Corrigida a tolerancia final de chegada melee para nao estacionar fora do alcance de ataque.

Testes executados: guerreiro individual; alcance final e pausa durante golpe; evento duplicado sem dano extra; cooldown; alvo escapando do alcance; cancelamento por morte; oito melee com destinos distintos; flanco lateral e aproximacao final; ranged aproximando/recuando; recuo junto da borda; contorno de cobertura para obter LOS; 25 agentes mistos sem sobreposicao persistente e dentro do NavMesh; alvo deslocado como dash; morte liberando reservas e colliders. Grupo misto: 1000 decisoes e 265 caminhos em 14 s simulados (nao e benchmark de CPU).

Arquivos do checkpoint: Assets/Scripts/Enemy/EnemyAI.cs, Assets/Tests/Editor/EnemyNavigationChecks.cs (+ meta), Docs/Validation/EnemyAI-PlayMode.xml e este documento. Compilacao Roslyn contra assemblies da Unity passou; avisos anteriores permanecem. Cena, ondas, prefabs, animacoes, vida/dano, camera e HUD nao foram regravados.

Falta: CP2 comando de Editor para aplicar HP/velocidades/perfis nos tres prefabs referenciados pelo WaveManager e ajustar apenas locomocao; CP3 regressao, instrucoes e relatorios. Testes visuais na arena aprovada, Hit/Death com os modelos reais e sensacao de combate continuam pendentes; a suite usa atores sinteticos para isolar navegacao e dano/eventos.

## CP2 concluido — configuracao segura dos prefabs

CP1 registrado no commit local e4de539. Novos arquivos: Assets/Editor/EnemyAISetup.cs e Assets/Tests/Editor/EnemyAISetupChecks.cs (ambos com meta), Docs/Validation/EnemyAI-Presets-EditMode.xml. Modificado Assets/Editor/SurvivalGameSetup.cs somente nos valores iniciais dos inimigos e na taxa de locomocao para futuras geracoes.

Tools > Underdog > Update Enemy AI Presets resolve os tres prefabs reais do WaveManager da cena ativa; valida papeis/componentes/Controllers antes de escrever e pede confirmacao com os caminhos exatos. Cria backups dos prefabs e Controllers copiados em Assets/Generated/EnemyAI_<data>. Aplica Warrior 175 HP/2.5, Rogue 60 HP/5.5, Mage 75 HP/2.6 e distancias 6/7.5/9; configura separacao e decisoes. Somente o estado Locomotion passa a usar LocomotionRate. Clips, eventos, velocidades dos ataques, Hit/Death, dano, cooldown e referencias do manager continuam existentes. Em falha durante gravacao, restaura prefabs mantendo seus .meta/GUIDs. Nao altera cenas nem assets KayKit.

Validacao: compilacao de todos os scripts contra Unity 2022.3.62f3 PASS; runner EditMode PASS em prefabs sinteticos (perfis, backups, GUIDs mantidos, Controllers originais preservados, ataque/evento/Hit/Death preservados, rejeicao de prefab duplicado). Relatorio XML anexado. Nao aplicado ainda aos prefabs da partida real: executar o menu na cena UnderdogSurvival, fora do Play Mode. Nao executar Prepare Survival Game para aplicar somente IA.

Falta CP3: regressao de sobrevivencia/combate, verificacao dos hashes de arquivos protegidos, documentacao de parametros e testes de arena. Validacao visual e balanceamento dos modelos reais ainda precisam de Play Mode interativo.

## CP3 concluido — regressao e entrega (2026-11-15)

CP2 registrado no commit local 30e7d51. Arquivos desta etapa: Docs/EnemyAI.md (instrucao, parametros e roteiro manual), relatorios XML em Docs/Validation, EnemyAISetup.cs/EnemyAISetupChecks.cs (recusa de alteracoes nao salvas no prefab/Controller/Prefab Mode) e EnemyAI.cs (restaurada acentuacao original de comentarios/rotulos). Nao houve outra mudanca de gameplay nesta etapa.

Validacoes finais executadas:
- Compilacao de todos os scripts runtime/Editor contra assemblies Unity 2022.3.62f3: PASS; somente os dois avisos preexistentes.
- Runner Unity EditMode em projeto isolado: 3/3 PASS. Presets via AssetDatabase/PrefabUtility, preservacao de backup/GUID/Controller/eventos/Hit/Death, slots duplicados e assets com alteracoes nao salvas rejeitados; regressao de vida e PlayerCombatSelfCheck existente.
- Runner Unity PlayMode em projeto isolado: 2/2 PASS. Suite nova de navegacao com colisores/agentes reais e suite SurvivalChecks existente (projeteis 60/10 FPS, dano unico, area, cinco ondas vazias/vitoria/derrota). Grupo de 25: 1000 decisoes, 259 caminhos em 14 s simulados no ultimo run; nao e medicao de CPU/GPU.
- Comparacao SHA256 de 3700 arquivos protegidos: zero diferencas. Diff contra baseline 4011aaf tambem sem alteracao em cenas, assets gerados/originais, ondas, jogador, combate/vida, camera, HUD, pacotes ou ProjectSettings.
- git diff --check: PASS. Nenhum push ou merge realizado.

Para aplicar: abrir UnderdogSurvival fora do Play Mode, salvar trabalho pendente nos assets, Tools > Underdog > Update Enemy AI Presets e conferir/confirmar os tres caminhos exibidos. O menu faz os backups e aplica os perfis; a partida/cena real nao foi regravada por esta sessao. NAO executar Prepare Survival Game para esta atualizacao. Nao precisa rebake do NavMesh existente.

Pendentes exclusivamente no Editor interativo: validar sensacao/animacoes dos modelos reais, Mage junto de paredes/colunas reais, dash real do jogador no grupo, tres magias, cinco ondas completas com combate, derrota/reinicio e Profiler com 15-25 agentes. Roteiro e todos os parametros em Docs/EnemyAI.md. Nao declarar esses testes manuais concluidos com base na compilacao ou nas fixtures sinteticas. Mudanca de HP/velocidade pode exigir ajuste fino posterior sem alterar a composicao das ondas.

Estado de retomada: runtime, ferramenta segura e suites concluidos; nao refazer CP1/CP2. Proxima sessao deve aplicar/validar na arena e corrigir apenas problemas observados. O arquivo nao rastreado Assets/Generated/UnderdogSurvival/EditorBuildSettings.backup.txt.meta apareceu no import do Editor; foi preservado e ficou fora dos commits de IA. Commits locais de implementacao: e4de539 e 30e7d51; o commit deste checkpoint sucede ambos.

## CP4 concluido — partida bloqueada pelo Pause do Editor (2026-11-15)

Usuario relatou que Iniciar/Enter nao respondiam e confirmou que o botao Pause da Unity estava azul. Com o Editor pausado, os frames de gameplay e callbacks de inicio nao avancam. Nao foi identificado defeito novo no binding do botao ou na progressao das ondas. O log do Editor tambem registra NullReferenceException em CombatFeedback.Restore/OnDisable apos recompilacoes; essa excecao pode acionar Error Pause. Nao foi possivel inspecionar ou alterar o estado vivo do Editor por Pipeline (indisponivel nesta Unity 2022.3).

Causa da excecao reproduzida: durante recarga de scripts, Renderer[] pode ser restaurado sem o cache nativo MaterialPropertyBlock[]; Restore verificava apenas renderers, mas acessava originais[i]. Agora recusa cache nulo/incompleto e entradas nulas, mantendo a restauracao normal quando o cache existe. Mudanca restrita a CombatFeedback.cs; nenhum ajuste em WaveManager, HUD, input, IA ou configuracao de ondas.

Arquivos: Assets/Scripts/Combat/CombatFeedback.cs; Assets/Tests/Editor/CombatFeedbackSelfCheck.cs (+ meta); Docs/Validation/StartResume-EditMode.xml; este checkpoint. Menu de verificacao: Tools > Underdog > Check Combat Feedback (Edit Mode).

Validacoes: regressao nova falhou antes da correcao com a mesma NullReferenceException da linha 48 (XML anterior guardado em Temp/StartResumeCheck/feedback-before.xml); apos correcao, runner Unity 2022.3 EditMode 4/4 PASS, incluindo vida, PlayerCombat e presets de IA existentes. Testa cache ausente, vazio, entradas nulas, Renderer removido e restauracao normal de propriedades. Compilacao de todos os scripts passou, com os dois avisos anteriores. Nao equivale a teste interativo de inicio da partida.

Alteracoes locais encontradas e preservadas: Melee/Fast/Ranged.prefab, UnderdogSurvival.unity (ajuste de fonte), nova pasta EnemyAI_20261115_143647 e seus metas. Nao inclui-las no commit desta correcao. Checkpoint anterior d667a11 permanece como baseline de IA.

Aplicacao/teste manual pendente: parar Play, aguardar compilacao, limpar erros antigos do Console, entrar novamente em Play e garantir que Pause nao esteja azul. Testar Iniciar, repetir com Enter e observar o comeco da primeira onda. Error Pause pode continuar habilitado; nao ocultar erros novos. Na proxima sessao, consultar este checkpoint e confirmar o teste no Editor antes de investigar outros sistemas.
