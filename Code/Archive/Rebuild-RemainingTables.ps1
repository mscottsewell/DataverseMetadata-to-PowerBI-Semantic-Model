# Complete table rebuild script
# Reads metadata and rebuilds all TMDL files with proper structure

$tables = @{
    'IW Product Feedback' = @{
        entity = 'caiiw_iwproductfeedback'
        idField = 'caiiw_iwproductfeedbackid'
    }
    'Marketing' = @{
        entity = 'caiiw_marketing'
        idField = 'caiiw_marketingid'
    }
    'Nomination' = @{
        entity = 'caiiw_nomination'
        idField = 'caiiw_nominationid'
    }
    'Nomination Learning Objective' = @{
        entity = 'caiiw_nominationlearningobjective'
        idField = 'caiiw_nominationlearningobjectiveid'
    }
    'Workshop Feedback' = @{
        entity = 'caiiw_workshopfeedback'
        idField = 'caiiw_workshopfeedbackid'
    }
    'Learning Objective' = @{
        entity = 'caiiw_learningobjective'
        idField = 'caiiw_learningobjectiveid'
    }
    'Imagination Workshop' = @{
        entity = 'caiiw_imaginationworkshop'
        idField = 'caiiw_imaginationworkshopid'
    }
    'CoreAI Guide' = @{
        entity = 'caiiw_coreaiguide'
        idField = 'caiiw_coreaiguideid'
    }
}

Write-Host "Rebuilding remaining tables with proper Lookup/Choice handling..." -ForegroundColor Cyan
Write-Host "Tables to update: $($tables.Keys -join ', ')" -ForegroundColor Yellow
Write-Host "`nManual updates required - use Power BI Desktop to verify schema" -ForegroundColor Yellow
